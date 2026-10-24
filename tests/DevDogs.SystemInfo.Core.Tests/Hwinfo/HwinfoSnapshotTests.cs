using DevDogs.SystemInfo.Core.Tests.Fixtures;
using DevDogs.SystemInfo.Hwinfo;

namespace DevDogs.SystemInfo.Core.Tests.Hwinfo;

public class HwinfoSnapshotTests
{
    [Fact]
    public void Parses_the_real_layout_back_into_the_same_snapshot()
    {
        var expected = Fixture.IntelAmdGpu;

        var parsed = HwinfoSnapshot.Parse(expected.Header, Fixture.ToBlock(expected));

        Assert.Equal(expected.Header, parsed.Header);
        Assert.Equal(expected.Sensors, parsed.Sensors);
        Assert.Equal(expected.Readings, parsed.Readings);
    }

    [Fact]
    public void Reads_names_units_and_values_of_known_readings()
    {
        var snapshot = HwinfoSnapshot.Parse(Fixture.IntelAmdGpu.Header, Fixture.ToBlock(Fixture.IntelAmdGpu));

        var package = snapshot.Readings.Single(r => r.Label == "CPU Package Power");
        Assert.Equal(ReadingType.Power, package.Type);
        Assert.Equal("W", package.Unit);
        Assert.Equal("CPU [#0]: Intel Core i5-14600K: Enhanced", snapshot.Sensors[package.SensorIndex].Name);
        Assert.InRange(package.Value, 1, 300);
    }

    [Fact]
    public void Read_returns_null_when_the_block_is_shorter_than_the_header_says()
    {
        var snapshot = Fixture.IntelAmdGpu;
        var block = Fixture.ToBlock(snapshot);
        using var memory = new FakeSharedMemory(block[..^460]);

        Assert.Null(HwinfoSnapshot.Read(memory, snapshot.Header));
    }

    [Fact]
    public void Read_copies_the_whole_block()
    {
        var snapshot = Fixture.IntelAmdGpu;
        using var memory = new FakeSharedMemory(Fixture.ToBlock(snapshot));

        Assert.Equal(snapshot.Readings, HwinfoSnapshot.Read(memory, snapshot.Header)!.Readings);
    }
}
