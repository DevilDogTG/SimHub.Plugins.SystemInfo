using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DevDogs.SystemInfo.Hwinfo;

namespace DevDogs.SystemInfo.Matching;

/// <summary>Which hardware a rule looks at. Resolved per snapshot to concrete sensors.</summary>
public enum SensorRole
{
    /// <summary>Main CPU sensor, <c>CPU [#0]: Intel Core i5-14600K</c> (clocks, usage).</summary>
    Cpu,

    /// <summary><c>CPU [#0]: ...: Enhanced</c> (package temperature and power).</summary>
    CpuEnhanced,

    /// <summary>Every sensor of the primary CPU (<c>CPU [#0]: ...</c>, any suffix), Enhanced first.</summary>
    CpuAny,

    /// <summary>Primary graphics card: first <c>dGPU [#N]</c>, else first <c>GPU [#N]</c>, else <c>iGPU [#N]</c>.</summary>
    Gpu,

    /// <summary><c>System: ...</c> (physical and virtual memory).</summary>
    System,

    /// <summary><c>Memory Timings</c> (memory clock).</summary>
    MemoryTimings,

    /// <summary>RivaTuner Statistics Server framerate.</summary>
    Rtss,

    /// <summary>HWiNFO's PresentMon framerate (foreground app).</summary>
    PresentMon,

    /// <summary>Anything that isn't CPU, GPU or one of the above: motherboard Super I/O, drives, network.</summary>
    Board,
}

/// <summary>Picks the sensors behind each <see cref="SensorRole"/> from their original names (ADR-0003).</summary>
public sealed class SensorClassifier
{
    private static readonly Regex CpuName = new(@"^CPU \[#(?<n>\d+)\]: (?<model>[^:]+)(?<suffix>: .+)?$");
    private static readonly Regex GpuName = new(@"^(?<kind>dGPU|GPU|iGPU) \[#(?<n>\d+)\]: (?<model>[^:]+)(: .+)?$");

    private readonly Dictionary<SensorRole, IReadOnlyList<HwinfoSensor>> _roles = new();

    public SensorClassifier(IReadOnlyList<HwinfoSensor> sensors)
    {
        var cpus = sensors
            .Select(s => (Sensor: s, Match: CpuName.Match(s.Name)))
            .Where(x => x.Match.Success)
            .ToList();
        var primaryCpu = cpus.Select(x => x.Match.Groups["n"].Value).OrderBy(int.Parse).FirstOrDefault();
        var cpuFamily = cpus.Where(x => x.Match.Groups["n"].Value == primaryCpu).ToList();

        var main = cpuFamily.FirstOrDefault(x => !x.Match.Groups["suffix"].Success);
        CpuModel = main.Match?.Groups["model"].Value.Trim();
        var enhanced = cpuFamily.Where(x => x.Match.Groups["suffix"].Value == ": Enhanced").Select(x => x.Sensor).ToList();

        _roles[SensorRole.Cpu] = main.Sensor is null ? [] : [main.Sensor];
        _roles[SensorRole.CpuEnhanced] = enhanced;
        _roles[SensorRole.CpuAny] = enhanced.Concat(cpuFamily.Select(x => x.Sensor).Except(enhanced)).ToList();

        var gpus = sensors
            .Select(s => (Sensor: s, Match: GpuName.Match(s.Name)))
            .Where(x => x.Match.Success)
            .ToList();
        var gpu = gpus.FirstOrDefault(x => x.Match.Groups["kind"].Value == "dGPU");
        if (gpu.Sensor is null) gpu = gpus.FirstOrDefault(x => x.Match.Groups["kind"].Value == "GPU");
        if (gpu.Sensor is null) gpu = gpus.FirstOrDefault();
        GpuModel = gpu.Match?.Groups["model"].Value.Trim();
        _roles[SensorRole.Gpu] = gpu.Sensor is null ? [] : [gpu.Sensor];

        _roles[SensorRole.System] = sensors.Where(s => s.Name.StartsWith("System: ")).ToList();
        _roles[SensorRole.MemoryTimings] = sensors.Where(s => s.Name == "Memory Timings").ToList();
        _roles[SensorRole.Rtss] = sensors.Where(s => s.Name == "RTSS").ToList();
        _roles[SensorRole.PresentMon] = sensors.Where(s => s.Name.StartsWith("PresentMon")).ToList();

        var classified = new HashSet<HwinfoSensor>(_roles.Values.SelectMany(x => x).Concat(cpus.Select(x => x.Sensor)).Concat(gpus.Select(x => x.Sensor)));
        _roles[SensorRole.Board] = sensors.Where(s => !classified.Contains(s)).ToList();
    }

    /// <summary>e.g. <c>Intel Core i5-14600K</c>; <c>null</c> when HWiNFO reports no CPU sensor.</summary>
    public string? CpuModel { get; }

    /// <summary>e.g. <c>AMD Radeon RX 9070 XT</c> (board name dropped); <c>null</c> without a GPU sensor.</summary>
    public string? GpuModel { get; }

    public IReadOnlyList<HwinfoSensor> Sensors(SensorRole role) => _roles[role];
}
