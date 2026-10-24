using System;
using System.Collections.Generic;
using System.Management;
using DevDogs.SystemInfo.Hardware;
using Microsoft.Win32;

namespace DevDogs.SystemInfo;

/// <summary>Reads what HWiNFO's shared memory lacks from Windows: VRAM size (registry) and RAM modules (WMI).</summary>
internal static class StaticHardwareProbe
{
    // Display adapter device class. Its HardwareInformation.qwMemorySize is the full dedicated VRAM; WMI's
    // Win32_VideoController.AdapterRAM is a 32-bit value and caps at 4 GB.
    private const string DisplayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public static StaticHardware Probe() => new(ReadAdapters(), ReadModules());

    private static IReadOnlyList<DisplayAdapter> ReadAdapters()
    {
        var adapters = new List<DisplayAdapter>();
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(DisplayClassKey);
            if (classKey is null) return adapters;

            foreach (var name in classKey.GetSubKeyNames())
            {
                if (name.Length != 4 || !int.TryParse(name, out _)) continue; // 0000, 0001 ...; skip "Properties"
                using var device = classKey.OpenSubKey(name);
                if (device?.GetValue("DriverDesc") is not string desc) continue;

                var size = ToBytes(device.GetValue("HardwareInformation.qwMemorySize"))
                           ?? ToBytes(device.GetValue("HardwareInformation.MemorySize"));
                if (size is > 0) adapters.Add(new DisplayAdapter(desc, size.Value));
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            SimHub.Logging.Current.Warn($"DevDogs System Info: cannot read display adapters: {ex.Message}");
        }
        return adapters;
    }

    private static ulong? ToBytes(object? value) => value switch
    {
        long l => (ulong)l,
        int i => (uint)i,
        byte[] { Length: >= 8 } b => BitConverter.ToUInt64(b, 0),
        byte[] { Length: >= 4 } b => BitConverter.ToUInt32(b, 0),
        _ => null,
    };

    private static IReadOnlyList<MemoryModule> ReadModules()
    {
        var modules = new List<MemoryModule>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Capacity, SMBIOSMemoryType, ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory");
            foreach (var item in searcher.Get())
            {
                using (item)
                {
                    var speed = Convert.ToInt32(item["ConfiguredClockSpeed"] ?? item["Speed"] ?? 0);
                    modules.Add(new MemoryModule(
                        Convert.ToUInt64(item["Capacity"] ?? 0UL),
                        Convert.ToInt32(item["SMBIOSMemoryType"] ?? 0),
                        speed));
                }
            }
        }
        catch (ManagementException ex)
        {
            SimHub.Logging.Current.Warn($"DevDogs System Info: cannot read memory modules: {ex.Message}");
        }
        return modules;
    }
}
