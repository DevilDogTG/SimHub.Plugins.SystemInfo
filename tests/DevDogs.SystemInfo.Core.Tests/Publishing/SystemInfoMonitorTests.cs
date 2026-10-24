using DevDogs.SystemInfo.Core.Tests.Fixtures;
using DevDogs.SystemInfo.Core.Tests.Hwinfo;
using DevDogs.SystemInfo.Hardware;
using DevDogs.SystemInfo.Hwinfo;
using DevDogs.SystemInfo.Matching;
using DevDogs.SystemInfo.Publishing;
using P = DevDogs.SystemInfo.Publishing.PublishedProperties;

namespace DevDogs.SystemInfo.Core.Tests.Publishing;

public class SystemInfoMonitorTests
{
    private static readonly HwinfoSnapshot Snapshot = Fixture.IntelAmdGpu;
    private static readonly StaticHardware OwnerPc = new(
        [new DisplayAdapter("AMD Radeon RX 9070 XT", 17_095_983_104)],
        Enumerable.Repeat(new MemoryModule(16UL << 30, 26, 3200), 4).ToList());

    private readonly FakeOpener _opener = new();
    private readonly List<bool> _events = [];
    private TimeSpan _now = TimeSpan.Zero;
    private Dictionary<string, ReadingSelector> _overrides = [];

    private SystemInfoMonitor Start()
    {
        var monitor = new SystemInfoMonitor(new HwinfoConnection(_opener, () => _now), () => _overrides) { Hardware = OwnerPc };
        monitor.ConnectionChanged += _events.Add;
        return monitor;
    }

    private const string Enhanced = "CPU [#0]: Intel Core i5-14600K: Enhanced";

    /// <summary>Value of a reading; <paramref name="sensor"/> disambiguates labels that several sensors share (CPU Package).</summary>
    private static double Reading(string label, string? sensor = null) => Snapshot.Readings
        .First(r => r.Label == label && (sensor is null || Snapshot.Sensors[r.SensorIndex].Name == sensor)).Value;

    private static HwinfoSnapshot With(HwinfoSnapshot snapshot, string label, double value, long pollTime) => snapshot with
    {
        Header = snapshot.Header with { PollTime = pollTime },
        Readings = snapshot.Readings.Select(r => r.Label == label ? r with { Value = value } : r).ToList(),
    };

    [Fact]
    public void Starts_disconnected_with_every_key_present()
    {
        using var monitor = Start();

        Assert.Equal(P.Keys.Count, monitor.Values.Count);
        Assert.Equal(false, monitor.Values[P.Connected]);
        Assert.All(P.Keys.Where(k => k != P.Connected), k => Assert.Null(monitor.Values[k]));
    }

    [Fact]
    public void Publishes_readings_names_and_derived_values_once_connected()
    {
        _opener.Next = new FakeSharedMemory(Fixture.ToBlock(Snapshot));
        using var monitor = Start();

        monitor.Tick();
        var v = monitor.Values;

        Assert.Equal(true, v[P.Connected]);
        Assert.Equal([true], _events);
        Assert.Equal("Intel Core i5-14600K", v[P.CpuName]);
        Assert.Equal("AMD Radeon RX 9070 XT", v[P.GpuName]);
        Assert.Equal(Reading("CPU Package", Enhanced), v["CPU.Temperature"]);
        Assert.Equal(Reading("Physical Memory Used") + Reading("Physical Memory Available"), v[P.RamTotal]);
        Assert.Equal(Reading("Memory Clock") * 2, v[P.RamSpeed]);
        Assert.Equal(16_304, (double)v[P.VramTotal]!, precision: 0);
        Assert.Equal(Reading("GPU D3D Memory Dedicated") / 16_304.0 * 100, (double)v[P.VramLoad]!, precision: 2);
        Assert.Equal("DDR4", v[P.RamType]);
        Assert.Equal("64 GB DDR4-3200", v[P.RamDescription]);
        Assert.DoesNotContain("RAM.Clock", v.Keys);
    }

    [Fact]
    public void Skips_reading_until_HWiNFO_stamps_a_new_update()
    {
        var memory = new FakeSharedMemory(Fixture.ToBlock(Snapshot));
        _opener.Next = memory;
        using var monitor = Start();
        monitor.Tick();

        // Same poll time: value changed in memory but HWiNFO hasn't finished an update yet.
        memory.Bytes = Fixture.ToBlock(With(Snapshot, "CPU Package", 99, Snapshot.Header.PollTime));
        monitor.Tick();
        Assert.Equal(Reading("CPU Package", Enhanced), monitor.Values["CPU.Temperature"]);

        memory.Bytes = Fixture.ToBlock(With(Snapshot, "CPU Package", 99, Snapshot.Header.PollTime + 2));
        monitor.Tick();
        Assert.Equal(99.0, monitor.Values["CPU.Temperature"]);
    }

    [Fact]
    public void HWiNFO_closing_clears_values_and_reconnects_later()
    {
        var memory = new FakeSharedMemory(Fixture.ToBlock(Snapshot));
        _opener.Next = memory;
        using var monitor = Start();
        monitor.Tick();

        memory.Bytes = HeaderBytes.Build(HwinfoHeader.DeadSignature);
        monitor.Tick();
        Assert.Equal(false, monitor.Values[P.Connected]);
        Assert.Null(monitor.Values["GPU.HotSpot"]);
        Assert.Null(monitor.Resolution);

        _opener.Next = new FakeSharedMemory(Fixture.ToBlock(Snapshot));
        _now += HwinfoConnection.RetryInterval;
        monitor.Tick();
        Assert.Equal(true, monitor.Values[P.Connected]);
        Assert.Equal([true, false, true], _events);
    }

    [Fact]
    public void Rebinds_when_a_reading_moved_under_the_same_layout()
    {
        var memory = new FakeSharedMemory(Fixture.ToBlock(Snapshot));
        _opener.Next = memory;
        using var monitor = Start();
        monitor.Tick();

        // Swap two readings' slots (same counts, different IDs at the bound index), as after a HWiNFO restart.
        var hotSpot = Snapshot.Readings.Single(r => r.Label == "GPU Hot Spot Temperature");
        var gpuTemp = Snapshot.Readings.Single(r => r.Label == "GPU Temperature");
        var swapped = Snapshot with
        {
            Header = Snapshot.Header with { PollTime = Snapshot.Header.PollTime + 2 },
            Readings = Snapshot.Readings
                .Select(r => r.Index == hotSpot.Index ? gpuTemp with { Index = r.Index }
                           : r.Index == gpuTemp.Index ? hotSpot with { Index = r.Index } : r)
                .ToList(),
        };
        memory.Bytes = Fixture.ToBlock(swapped);
        monitor.Tick();

        Assert.Equal(hotSpot.Value, monitor.Values["GPU.HotSpot"]);
        Assert.Equal(gpuTemp.Index, monitor.Resolution!.Properties["GPU.HotSpot"].Readings.Single().Index);
    }

    [Fact]
    public void Invalidate_applies_changed_overrides_on_the_next_tick()
    {
        _opener.Next = new FakeSharedMemory(Fixture.ToBlock(Snapshot));
        using var monitor = Start();
        monitor.Tick();

        _overrides = new() { ["CPU.Temperature"] = new("CPU [#0]: Intel Core i5-14600K: DTS", 0, "Core Max") };
        monitor.Invalidate();
        monitor.Tick();

        var property = monitor.Resolution!.Properties["CPU.Temperature"];
        Assert.Equal(ResolutionSource.Override, property.Source);
        Assert.Equal(Reading("Core Max"), monitor.Values["CPU.Temperature"]);
    }
}
