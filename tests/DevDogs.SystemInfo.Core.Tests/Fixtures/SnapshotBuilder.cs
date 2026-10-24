using DevDogs.SystemInfo.Hwinfo;

namespace DevDogs.SystemInfo.Core.Tests.Fixtures;

/// <summary>Builds small snapshots for hardware we have no recorded dump of.</summary>
internal sealed class SnapshotBuilder
{
    private readonly List<HwinfoSensor> _sensors = [];
    private readonly List<HwinfoReading> _readings = [];

    public SnapshotBuilder Sensor(string name, uint instance = 0)
    {
        _sensors.Add(new HwinfoSensor(_sensors.Count, 0xF0000000u + (uint)_sensors.Count, instance, name, name));
        return this;
    }

    /// <summary>Adds a reading to the last sensor.</summary>
    public SnapshotBuilder Reading(ReadingType type, string label, string unit, double value)
    {
        _readings.Add(new HwinfoReading(_readings.Count, type, _sensors.Count - 1, (uint)_readings.Count, label, label, unit, value));
        return this;
    }

    public HwinfoSnapshot Build() => new(Fixture.IntelAmdGpu.Header, _sensors, _readings);
}
