using DevDogs.SystemInfo.Core.Tests.Fixtures;
using DevDogs.SystemInfo.Hwinfo;
using DevDogs.SystemInfo.Matching;

namespace DevDogs.SystemInfo.Core.Tests.Matching;

public class SensorResolverTests
{
    private static Resolution Resolve(HwinfoSnapshot snapshot, IReadOnlyDictionary<string, ReadingSelector>? overrides = null) =>
        SensorResolver.Resolve(snapshot, BuiltInRules.All, overrides);

    private static double? Value(Resolution resolution, HwinfoSnapshot snapshot, string key) =>
        resolution.Properties[key].Evaluate(i => snapshot.Readings[i].Value);

    // ---- Owner's PC (recorded): Intel Core i5-14600K, AMD RX 9070 XT, MSI B760M ----

    [Theory]
    [InlineData("CPU.Temperature", "CPU [#0]: Intel Core i5-14600K: Enhanced", "CPU Package")]
    [InlineData("CPU.Load", "CPU [#0]: Intel Core i5-14600K", "Total CPU Usage")]
    [InlineData("CPU.Power", "CPU [#0]: Intel Core i5-14600K: Enhanced", "CPU Package Power")]
    [InlineData("CPU.FanSpeed", "MSI PRO B760M-A WIFI DDR4 (MS-7D99) (Nuvoton NCT6687D)", "CPU")]
    [InlineData("GPU.Temperature", "dGPU [#1]: AMD Radeon RX 9070 XT: Sapphire Radeon RX 9070 XT PULSE", "GPU Temperature")]
    [InlineData("GPU.HotSpot", "dGPU [#1]: AMD Radeon RX 9070 XT: Sapphire Radeon RX 9070 XT PULSE", "GPU Hot Spot Temperature")]
    [InlineData("GPU.MemoryTemperature", "dGPU [#1]: AMD Radeon RX 9070 XT: Sapphire Radeon RX 9070 XT PULSE", "GPU Memory Junction Temperature")]
    [InlineData("GPU.Load", "dGPU [#1]: AMD Radeon RX 9070 XT: Sapphire Radeon RX 9070 XT PULSE", "GPU Utilization")]
    [InlineData("GPU.Power", "dGPU [#1]: AMD Radeon RX 9070 XT: Sapphire Radeon RX 9070 XT PULSE", "Total Board Power (TBP)")]
    [InlineData("GPU.FanSpeed", "dGPU [#1]: AMD Radeon RX 9070 XT: Sapphire Radeon RX 9070 XT PULSE", "GPU Fan")]
    [InlineData("GPU.Clock", "dGPU [#1]: AMD Radeon RX 9070 XT: Sapphire Radeon RX 9070 XT PULSE", "GPU Clock")]
    [InlineData("GPU.MemoryClock", "dGPU [#1]: AMD Radeon RX 9070 XT: Sapphire Radeon RX 9070 XT PULSE", "GPU Memory Clock")]
    [InlineData("GPU.VRAM.Used", "dGPU [#1]: AMD Radeon RX 9070 XT: Sapphire Radeon RX 9070 XT PULSE", "GPU D3D Memory Dedicated")]
    [InlineData("GPU.VRAM.Dynamic", "dGPU [#1]: AMD Radeon RX 9070 XT: Sapphire Radeon RX 9070 XT PULSE", "GPU D3D Memory Dynamic")]
    [InlineData("RAM.Used", "System: MSI MS-7D99", "Physical Memory Used")]
    [InlineData("RAM.Free", "System: MSI MS-7D99", "Physical Memory Available")]
    [InlineData("RAM.Load", "System: MSI MS-7D99", "Physical Memory Load")]
    [InlineData("RAM.Clock", "Memory Timings", "Memory Clock")]
    [InlineData("FPS.Current", "RTSS", "Framerate")]
    [InlineData("FPS.Low1", "RTSS", "Framerate 1% Low")]
    public void Owner_pc_binds_each_property_to_the_expected_reading(string key, string sensor, string label)
    {
        var property = Resolve(Fixture.IntelAmdGpu).Properties[key];

        var reading = Assert.Single(property.Readings);
        Assert.Equal(sensor, reading.Sensor.Name);
        Assert.Equal(label, reading.Label);
        Assert.Equal(ResolutionSource.Rule, property.Source);
    }

    [Fact]
    public void Owner_pc_resolves_every_built_in_property()
    {
        var unresolved = Resolve(Fixture.IntelAmdGpu).Properties.Values.Where(p => !p.IsResolved).Select(p => p.Rule.Key);

        Assert.Empty(unresolved);
    }

    [Fact]
    public void Owner_pc_names_cpu_and_gpu_without_board_suffix()
    {
        var resolution = Resolve(Fixture.IntelAmdGpu);

        Assert.Equal("Intel Core i5-14600K", resolution.CpuModel);
        Assert.Equal("AMD Radeon RX 9070 XT", resolution.GpuModel);
    }

    [Fact]
    public void Cpu_clock_is_the_highest_of_all_p_and_e_core_clocks()
    {
        var snapshot = Fixture.IntelAmdGpu;
        var resolution = Resolve(snapshot);
        var clock = resolution.Properties["CPU.Clock"];

        Assert.Equal(14, clock.Readings.Count); // 6 P-cores + 8 E-cores on an i5-14600K
        var expected = snapshot.Readings.Where(r => r.Label.EndsWith(" Clock") && r.Label.Contains("-core ")).Max(r => r.Value);
        Assert.Equal(expected, Value(resolution, snapshot, "CPU.Clock"));
    }

    [Fact]
    public void Discrete_gpu_wins_over_the_integrated_one()
    {
        Assert.StartsWith("dGPU [#1]", Resolve(Fixture.IntelAmdGpu).Properties["GPU.Load"].Readings[0].Sensor.Name);
    }

    [Fact]
    public void Without_rtss_fps_falls_back_to_present_mon()
    {
        var full = Fixture.IntelAmdGpu;
        var rtss = full.Sensors.Single(s => s.Name == "RTSS").Index;
        var snapshot = full with { Readings = full.Readings.Where(r => r.SensorIndex != rtss).ToList() };

        var fps = Resolve(snapshot).Properties["FPS.Current"];

        Assert.Equal("Framerate Presented (avg)", fps.Readings.Single().Label);
    }

    // ---- Other vendors: labels follow HWiNFO's naming, not yet confirmed by a dump from real hardware ----

    private static HwinfoSnapshot AmdCpuNvidiaGpu() => new SnapshotBuilder()
        .Sensor("CPU [#0]: AMD Ryzen 7 7800X3D")
        .Reading(ReadingType.Clock, "Core 0 Clock (perf #2/2)", "MHz", 4950)
        .Reading(ReadingType.Clock, "Core 1 Clock (perf #1/1)", "MHz", 5050)
        .Reading(ReadingType.Clock, "Core 0 T0 Effective Clock", "MHz", 300)
        .Reading(ReadingType.Usage, "Total CPU Usage", "%", 12)
        .Sensor("CPU [#0]: AMD Ryzen 7 7800X3D: Enhanced")
        .Reading(ReadingType.Temperature, "CPU (Tctl/Tdie)", "C", 61.5)
        .Reading(ReadingType.Power, "CPU Package Power", "W", 48)
        .Sensor("ASUS ROG STRIX B650E-F GAMING WIFI (Nuvoton NCT6799D)")
        .Reading(ReadingType.Temperature, "CPU", "C", 55)
        .Reading(ReadingType.Fan, "CPU_FAN", "RPM", 1100)
        .Sensor("GPU [#0]: NVIDIA GeForce RTX 4080: ASUS TUF")
        .Reading(ReadingType.Temperature, "GPU Temperature", "C", 58)
        .Reading(ReadingType.Usage, "GPU Core Load", "%", 97)
        .Reading(ReadingType.Power, "GPU Power", "W", 290)
        .Reading(ReadingType.Fan, "GPU Fan1", "RPM", 1500)
        .Reading(ReadingType.Other, "GPU Memory Allocated", "MB", 9000)
        .Build();

    [Theory]
    [InlineData("CPU.Temperature", "CPU (Tctl/Tdie)")]
    [InlineData("CPU.Power", "CPU Package Power")]
    [InlineData("CPU.FanSpeed", "CPU_FAN")]
    [InlineData("GPU.Load", "GPU Core Load")]
    [InlineData("GPU.Power", "GPU Power")]
    [InlineData("GPU.FanSpeed", "GPU Fan1")]
    [InlineData("GPU.VRAM.Used", "GPU Memory Allocated")]
    public void Amd_cpu_and_nvidia_gpu_use_their_fallback_labels(string key, string label)
    {
        Assert.Equal(label, Resolve(AmdCpuNvidiaGpu()).Properties[key].Readings.Single().Label);
    }

    [Fact]
    public void Amd_cpu_clock_ignores_effective_clocks()
    {
        var snapshot = AmdCpuNvidiaGpu();
        var resolution = Resolve(snapshot);

        Assert.Equal(2, resolution.Properties["CPU.Clock"].Readings.Count);
        Assert.Equal(5050, Value(resolution, snapshot, "CPU.Clock"));
    }

    [Fact]
    public void Gpu_without_d_prefix_and_its_model_are_found()
    {
        Assert.Equal("NVIDIA GeForce RTX 4080", Resolve(AmdCpuNvidiaGpu()).GpuModel);
    }

    [Fact]
    public void Integrated_gpu_is_used_when_it_is_the_only_one()
    {
        var snapshot = new SnapshotBuilder()
            .Sensor("iGPU [#0]: Intel UHD Graphics")
            .Reading(ReadingType.Usage, "GPU D3D Usage", "%", 4)
            .Build();

        Assert.Equal("GPU D3D Usage", Resolve(snapshot).Properties["GPU.Load"].Readings.Single().Label);
    }

    [Fact]
    public void Missing_hardware_leaves_properties_unresolved_with_no_value()
    {
        var snapshot = new SnapshotBuilder().Sensor("System: Test").Build();
        var resolution = Resolve(snapshot);

        Assert.False(resolution.Properties["GPU.Temperature"].IsResolved);
        Assert.Null(Value(resolution, snapshot, "GPU.Temperature"));
        Assert.Null(resolution.CpuModel);
    }

    // ---- Units and overrides ----

    [Fact]
    public void Fahrenheit_readings_are_published_in_celsius()
    {
        var snapshot = new SnapshotBuilder()
            .Sensor("GPU [#0]: NVIDIA GeForce RTX 4080")
            .Reading(ReadingType.Temperature, "GPU Temperature", "F", 140)
            .Build();

        Assert.Equal(60, Value(Resolve(snapshot), snapshot, "GPU.Temperature")!.Value, precision: 6);
    }

    [Fact]
    public void Override_replaces_the_built_in_match()
    {
        var overrides = new Dictionary<string, ReadingSelector>
        {
            ["CPU.Temperature"] = new("CPU [#0]: Intel Core i5-14600K: DTS", 0, "CPU Package"),
        };

        var property = Resolve(Fixture.IntelAmdGpu, overrides).Properties["CPU.Temperature"];

        Assert.Equal(ResolutionSource.Override, property.Source);
        Assert.Equal("CPU [#0]: Intel Core i5-14600K: DTS", property.Readings.Single().Sensor.Name);
    }

    [Fact]
    public void Override_for_hardware_that_is_gone_falls_back_and_is_flagged()
    {
        var overrides = new Dictionary<string, ReadingSelector>
        {
            ["CPU.FanSpeed"] = new("Old Board (ITE IT8688E)", 0, "CPU_FAN"),
        };

        var property = Resolve(Fixture.IntelAmdGpu, overrides).Properties["CPU.FanSpeed"];

        Assert.Equal(ResolutionSource.OverrideMissing, property.Source);
        Assert.Equal("CPU", property.Readings.Single().Label);
    }
}
