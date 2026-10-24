using System.Collections.Generic;
using System.Linq;
using DevDogs.SystemInfo.Hardware;
using DevDogs.SystemInfo.Matching;

namespace DevDogs.SystemInfo.Publishing;

/// <summary>
/// The property contract dashboards bind to (<c>DDSystemInfoPlugin.&lt;key&gt;</c>). Units: °C, %, MHz, W, RPM, MB, FPS;
/// <c>RAM.Speed</c> (live) and <c>RAM.RatedSpeed</c> are MT/s. Every value is <c>null</c> while HWiNFO is not connected or the hardware isn't found.
/// </summary>
public static class PublishedProperties
{
    public const string Connected = "Status.Connected";
    public const string CpuName = "CPU.Name";
    public const string GpuName = "GPU.Name";
    public const string VramTotal = "GPU.VRAM.Total";
    public const string VramLoad = "GPU.VRAM.Load";
    public const string RamTotal = "RAM.Total";
    public const string RamSpeed = "RAM.Speed";
    public const string RamType = "RAM.Type";
    public const string RamRatedSpeed = "RAM.RatedSpeed";
    public const string RamSpec = "RAM.Spec";
    public const string RamDescription = "RAM.Description";

    /// <summary>Rule-backed values used only to derive others; not published themselves.</summary>
    private static readonly HashSet<string> Internal = ["RAM.Clock"];

    /// <summary>Every published key, in dashboard-friendly order.</summary>
    public static IReadOnlyList<string> Keys { get; } =
        new[] { Connected, CpuName, GpuName }
            .Concat(BuiltInRules.All.Select(r => r.Key).Where(k => !Internal.Contains(k)))
            .Concat([VramTotal, VramLoad, RamTotal, RamSpeed, RamType, RamRatedSpeed, RamSpec, RamDescription])
            .ToList();

    /// <summary>Values for every key in <see cref="Keys"/>.</summary>
    /// <param name="resolution"><c>null</c> while disconnected.</param>
    /// <param name="values">Current rule-backed values from <see cref="LiveReader"/>.</param>
    public static Dictionary<string, object?> Compute(
        Resolution? resolution, IReadOnlyDictionary<string, double?> values, StaticHardware hardware)
    {
        var result = Keys.ToDictionary(k => k, _ => (object?)null);
        result[Connected] = resolution is not null;
        if (resolution is null) return result;

        double? V(string key) => values.TryGetValue(key, out var v) ? v : null;

        foreach (var key in values.Keys.Where(result.ContainsKey))
            result[key] = V(key);

        result[CpuName] = resolution.CpuModel;
        result[GpuName] = resolution.GpuModel;

        var vramTotal = hardware.VramTotalMb(resolution.GpuModel);
        result[VramTotal] = vramTotal;
        result[VramLoad] = V("GPU.VRAM.Used") is { } used && vramTotal is > 0 ? used / vramTotal * 100 : null;

        // OS-visible total, so RAM.Used / RAM.Total matches RAM.Load; installed size goes into RAM.Description.
        result[RamTotal] = V("RAM.Used") is { } ramUsed && V("RAM.Free") is { } ramFree ? ramUsed + ramFree : null;
        result[RamSpeed] = V("RAM.Clock") * 2; // DDR: two transfers per clock
        result[RamType] = hardware.RamType;
        result[RamRatedSpeed] = hardware.RamRatedSpeedMts;
        result[RamSpec] = hardware.RamSpec;
        result[RamDescription] = hardware.RamDescription;
        return result;
    }
}
