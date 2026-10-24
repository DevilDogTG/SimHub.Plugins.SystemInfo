using DevDogs.SystemInfo.Core.Tests.Fixtures;
using DevDogs.SystemInfo.Core.Tests.Hwinfo;
using DevDogs.SystemInfo.Hwinfo;
using DevDogs.SystemInfo.Matching;

namespace DevDogs.SystemInfo.Core.Tests.Matching;

public class LiveReaderTests
{
    private static readonly HwinfoSnapshot Snapshot = Fixture.IntelAmdGpu;

    private static (LiveReader Reader, Resolution Resolution) Bind() =>
        Bind(SensorResolver.Resolve(Snapshot, BuiltInRules.All));

    private static (LiveReader, Resolution) Bind(Resolution resolution) => (new LiveReader(resolution, Snapshot.Header), resolution);

    [Fact]
    public void Reads_the_same_values_the_snapshot_holds()
    {
        var (reader, resolution) = Bind();
        using var memory = new FakeSharedMemory(Fixture.ToBlock(Snapshot));

        var values = reader.Read(memory)!;

        foreach (var (key, property) in resolution.Properties)
            Assert.Equal(property.Evaluate(i => Snapshot.Readings[i].Value), values[key]);
    }

    [Fact]
    public void Picks_up_new_values_written_in_place()
    {
        var (reader, _) = Bind();
        var hotter = Snapshot with
        {
            Readings = Snapshot.Readings.Select(r => r.Label == "GPU Hot Spot Temperature" ? r with { Value = 88.5 } : r).ToList(),
        };
        using var memory = new FakeSharedMemory(Fixture.ToBlock(hotter));

        Assert.Equal(88.5, reader.Read(memory)!["GPU.HotSpot"]);
    }

    [Fact]
    public void Returns_null_when_a_bound_reading_was_replaced()
    {
        var (reader, _) = Bind();
        var moved = Snapshot with
        {
            Readings = Snapshot.Readings.Select(r => r.Label == "CPU Package Power" ? r with { Id = r.Id + 1 } : r).ToList(),
        };
        using var memory = new FakeSharedMemory(Fixture.ToBlock(moved));

        Assert.Null(reader.Read(memory));
    }

    [Fact]
    public void Fits_only_the_layout_it_was_bound_to()
    {
        var (reader, _) = Bind();

        Assert.True(reader.Fits(Snapshot.Header with { PollTime = 1 }));
        Assert.False(reader.Fits(Snapshot.Header with { ReadingCount = Snapshot.Header.ReadingCount + 1 }));
        Assert.False(reader.Fits(Snapshot.Header with { SensorCount = Snapshot.Header.SensorCount - 1 }));
    }
}
