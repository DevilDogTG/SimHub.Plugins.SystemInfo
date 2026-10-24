using System.Collections.Generic;
using DevDogs.SystemInfo.Hwinfo;
using static DevDogs.SystemInfo.Matching.SensorRole;

namespace DevDogs.SystemInfo.Matching;

/// <summary>
/// Default rules for Intel / AMD CPUs and AMD / NVIDIA GPUs. Candidates marked "seen" exist in a recorded fixture;
/// the others follow HWiNFO's naming for that vendor and still need a fixture from real hardware.
/// </summary>
public static class BuiltInRules
{
    public static IReadOnlyList<PropertyRule> All { get; } =
    [
        Rule("CPU.Temperature", Quantity.Temperature,
            C(CpuEnhanced, ReadingType.Temperature, @"^CPU Package$"),          // Intel, seen
            C(CpuEnhanced, ReadingType.Temperature, @"^CPU \(Tctl/Tdie\)$"),    // AMD
            C(CpuAny, ReadingType.Temperature, @"^CPU Package$"),
            C(CpuAny, ReadingType.Temperature, @"^CPU Die \(average\)$")),
        Rule("CPU.Load", Quantity.Percent,
            C(Cpu, ReadingType.Usage, @"^Total CPU Usage$")),                   // seen
        Rule("CPU.Clock", Quantity.Clock, Aggregate.Max,
            // P-core 0 Clock / E-core 6 Clock (Intel hybrid, seen), Core 0 Clock (older Intel), Core 0 Clock (perf #1/1) (AMD)
            C(Cpu, ReadingType.Clock, @"^(P-core|E-core|Core) \d+ Clock\b")),
        Rule("CPU.Power", Quantity.Power,
            C(CpuEnhanced, ReadingType.Power, @"^CPU Package Power$"),          // Intel seen, AMD same label
            C(CpuAny, ReadingType.Power, @"^CPU Package Power$")),
        Rule("CPU.FanSpeed", Quantity.FanSpeed,
            // Motherboard headers: CPU (MSI Nuvoton, seen), CPU Fan, CPU_FAN, CPU1 ... Overrides cover the rest.
            C(Board, ReadingType.Fan, @"(?i)^CPU([ _]?FAN)?\s*\d?$")),

        Rule("GPU.Temperature", Quantity.Temperature,
            C(Gpu, ReadingType.Temperature, @"^GPU Temperature$")),             // seen
        Rule("GPU.HotSpot", Quantity.Temperature,
            C(Gpu, ReadingType.Temperature, @"^GPU Hot ?Spot Temperature$")),   // seen
        Rule("GPU.MemoryTemperature", Quantity.Temperature,
            C(Gpu, ReadingType.Temperature, @"^GPU Memory Junction Temperature$"), // seen
            C(Gpu, ReadingType.Temperature, @"^GPU Memory Temperature$")),
        Rule("GPU.Load", Quantity.Percent,
            C(Gpu, ReadingType.Usage, @"^GPU Utilization$"),                    // AMD, seen
            C(Gpu, ReadingType.Usage, @"^GPU Core Load$"),                      // NVIDIA
            C(Gpu, ReadingType.Usage, @"^GPU D3D Usage$")),
        Rule("GPU.Power", Quantity.Power,
            C(Gpu, ReadingType.Power, @"^Total Board Power \(TBP\)$"),          // AMD, seen
            C(Gpu, ReadingType.Power, @"^GPU Power$"),                          // NVIDIA
            C(Gpu, ReadingType.Power, @"^Total Graphics Power \(TGP\)$"),       // AMD, seen
            C(Gpu, ReadingType.Power, @"^GPU ASIC Power$")),
        Rule("GPU.FanSpeed", Quantity.FanSpeed,
            C(Gpu, ReadingType.Fan, @"^GPU Fan\d?$")),                          // GPU Fan (AMD, seen), GPU Fan1 (NVIDIA)
        Rule("GPU.Clock", Quantity.Clock,
            C(Gpu, ReadingType.Clock, @"^GPU Clock$")),                         // seen
        Rule("GPU.MemoryClock", Quantity.Clock,
            C(Gpu, ReadingType.Clock, @"^GPU Memory Clock$")),                  // seen
        Rule("GPU.VRAM.Used", Quantity.Memory,
            C(Gpu, null, @"^GPU D3D Memory Dedicated$"),                        // from Windows, any vendor, seen
            C(Gpu, null, @"^GPU Memory Allocated$"),
            C(Gpu, null, @"^GPU Memory Usage$")),
        Rule("GPU.VRAM.Dynamic", Quantity.Memory,
            C(Gpu, null, @"^GPU D3D Memory Dynamic$")),                         // seen

        Rule("RAM.Used", Quantity.Memory,
            C(SensorRole.System, null, @"^Physical Memory Used$")),             // seen
        Rule("RAM.Free", Quantity.Memory,
            C(SensorRole.System, null, @"^Physical Memory Available$")),        // seen
        Rule("RAM.Load", Quantity.Percent,
            C(SensorRole.System, null, @"^Physical Memory Load$")),             // seen
        Rule("RAM.Clock", Quantity.Clock,
            C(MemoryTimings, ReadingType.Clock, @"^Memory Clock$")),            // seen; x2 for MT/s

        Rule("FPS.Current", Quantity.Framerate,
            C(Rtss, null, @"^Framerate$"),                                      // seen
            C(PresentMon, null, @"^Framerate Presented \(avg\)$")),             // seen
        Rule("FPS.Low1", Quantity.Framerate,
            C(Rtss, null, @"^Framerate 1% Low$"),                               // seen
            C(PresentMon, null, @"^Framerate Presented \(1% low\)$")),          // seen
    ];

    private static PropertyRule Rule(string key, Quantity quantity, params ReadingCandidate[] candidates) =>
        new(key, quantity, Aggregate.First, candidates);

    private static PropertyRule Rule(string key, Quantity quantity, Aggregate aggregate, params ReadingCandidate[] candidates) =>
        new(key, quantity, aggregate, candidates);

    private static ReadingCandidate C(SensorRole role, ReadingType? type, string label) => new(role, type, label);
}
