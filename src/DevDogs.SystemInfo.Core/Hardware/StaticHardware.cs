using System;
using System.Collections.Generic;
using System.Linq;

namespace DevDogs.SystemInfo.Hardware;

/// <summary>A display adapter as Windows lists it, with its dedicated memory size.</summary>
public sealed record DisplayAdapter(string Name, ulong MemoryBytes);

/// <summary>An installed memory module (SMBIOS, via WMI <c>Win32_PhysicalMemory</c>).</summary>
public sealed record MemoryModule(ulong CapacityBytes, int SmbiosMemoryType, int ConfiguredSpeedMts);

/// <summary>
/// Hardware facts HWiNFO's shared memory doesn't carry: total VRAM, installed RAM size and type.
/// Read once from Windows; they don't change while the PC runs.
/// </summary>
public sealed record StaticHardware(IReadOnlyList<DisplayAdapter> Adapters, IReadOnlyList<MemoryModule> Modules)
{
    public static StaticHardware None { get; } = new([], []);

    /// <summary>
    /// Dedicated VRAM of the adapter HWiNFO reports as the primary GPU (matched by name), else of the largest adapter.
    /// </summary>
    public double? VramTotalMb(string? gpuModel)
    {
        var adapter = gpuModel is null
            ? null
            : Adapters.FirstOrDefault(a => a.Name.IndexOf(gpuModel, StringComparison.OrdinalIgnoreCase) >= 0
                                           || gpuModel.IndexOf(a.Name, StringComparison.OrdinalIgnoreCase) >= 0);
        adapter ??= Adapters.OrderByDescending(a => a.MemoryBytes).FirstOrDefault();
        return adapter is null || adapter.MemoryBytes == 0 ? null : adapter.MemoryBytes / 1048576.0;
    }

    /// <summary>Installed RAM in MB (module capacity, not the OS-visible amount).</summary>
    public double? RamInstalledMb => Modules.Count == 0 ? null : Modules.Sum(m => (double)m.CapacityBytes) / 1048576.0;

    /// <summary><c>DDR4</c>, <c>DDR5</c>, ...; <c>null</c> when unknown or modules disagree.</summary>
    public string? RamType
    {
        get
        {
            var types = Modules.Select(m => MemoryTypeName(m.SmbiosMemoryType)).Distinct().ToList();
            return types.Count == 1 ? types[0] : null;
        }
    }

    /// <summary>e.g. <c>64 GB DDR4-3200</c>; parts that are unknown are left out.</summary>
    public string? RamDescription
    {
        get
        {
            if (RamInstalledMb is not { } mb) return null;
            var size = $"{Math.Round(mb / 1024):0} GB";
            return RamSpec is { } spec ? $"{size} {spec}" : size;
        }
    }

    /// <summary>
    /// Speed the modules run at as configured in firmware, in MT/s (the slowest module wins). It is the round rated
    /// figure; HWiNFO's live memory clock x 2 reads a little lower (3192 for DDR4-3200).
    /// </summary>
    public int? RamRatedSpeedMts
    {
        get
        {
            var speeds = Modules.Select(m => m.ConfiguredSpeedMts).Where(s => s > 0).ToList();
            return speeds.Count == 0 ? null : speeds.Min();
        }
    }

    /// <summary>e.g. <c>DDR4-3200</c>; just the type when the speed is unknown.</summary>
    public string? RamSpec => RamType is not { } type ? null : RamRatedSpeedMts is { } speed ? $"{type}-{speed}" : type;

    /// <summary>SMBIOS 3.x memory device type codes (table 76).</summary>
    public static string? MemoryTypeName(int smbiosType) => smbiosType switch
    {
        0x12 => "DDR",
        0x13 => "DDR2",
        0x18 => "DDR3",
        0x1A => "DDR4",
        0x1B => "LPDDR",
        0x1C => "LPDDR2",
        0x1D => "LPDDR3",
        0x1E => "LPDDR4",
        0x22 => "DDR5",
        0x23 => "LPDDR5",
        _ => null,
    };
}
