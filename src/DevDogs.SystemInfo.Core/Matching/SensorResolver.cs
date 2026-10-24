using System;
using System.Collections.Generic;
using System.Linq;
using DevDogs.SystemInfo.Hwinfo;

namespace DevDogs.SystemInfo.Matching;

/// <summary>
/// A reading chosen by the user on the settings page. Stored by original names (ADR-0003), so it survives HWiNFO
/// restarts and renames.
/// </summary>
public sealed record ReadingSelector(string SensorName, uint SensorInstance, string ReadingLabel);

public enum ResolutionSource
{
    /// <summary>A built-in rule matched.</summary>
    Rule,

    /// <summary>The user's override matched.</summary>
    Override,

    /// <summary>An override exists but its reading is gone (hardware changed); built-in rules were used instead.</summary>
    OverrideMissing,
}

/// <summary>One reading behind a property, with where it sits in shared memory for cheap re-reads.</summary>
public sealed record ResolvedReading(int Index, uint Id, HwinfoSensor Sensor, string Label, string Unit);

/// <summary>A property bound to its readings for the current HWiNFO session. No readings means no value.</summary>
public sealed record ResolvedProperty(PropertyRule Rule, IReadOnlyList<ResolvedReading> Readings, ResolutionSource Source)
{
    public bool IsResolved => Readings.Count > 0;

    /// <summary>Combines the readings' current values, already in the published unit.</summary>
    /// <param name="valueAt">Value of the reading at a shared-memory index.</param>
    public double? Evaluate(Func<int, double> valueAt)
    {
        if (Readings.Count == 0) return null;
        var values = Readings.Select(r => Units.Normalize(Rule.Quantity, r.Unit, valueAt(r.Index)));
        return Rule.Aggregate == Aggregate.Max ? values.Max() : values.First();
    }
}

/// <summary>Everything the plugin publishes for one HWiNFO session.</summary>
public sealed record Resolution(string? CpuModel, string? GpuModel, IReadOnlyDictionary<string, ResolvedProperty> Properties);

/// <summary>Binds properties to readings: the user's override first, then the built-in candidates in order.</summary>
public static class SensorResolver
{
    public static Resolution Resolve(
        HwinfoSnapshot snapshot,
        IReadOnlyList<PropertyRule> rules,
        IReadOnlyDictionary<string, ReadingSelector>? overrides = null)
    {
        var classifier = new SensorClassifier(snapshot.Sensors);
        var bySensor = snapshot.Readings.ToLookup(r => r.SensorIndex);

        var properties = new Dictionary<string, ResolvedProperty>();
        foreach (var rule in rules)
        {
            var source = ResolutionSource.Rule;
            if (overrides is not null && overrides.TryGetValue(rule.Key, out var selector))
            {
                var picked = FindOverride(snapshot, selector);
                if (picked is not null)
                {
                    properties[rule.Key] = new ResolvedProperty(rule, [picked], ResolutionSource.Override);
                    continue;
                }
                source = ResolutionSource.OverrideMissing;
            }

            properties[rule.Key] = new ResolvedProperty(rule, MatchRule(rule, classifier, bySensor, snapshot.Sensors), source);
        }

        return new Resolution(classifier.CpuModel, classifier.GpuModel, properties);
    }

    private static IReadOnlyList<ResolvedReading> MatchRule(
        PropertyRule rule, SensorClassifier classifier, ILookup<int, HwinfoReading> bySensor, IReadOnlyList<HwinfoSensor> sensors)
    {
        foreach (var candidate in rule.Candidates)
        {
            var matches = classifier.Sensors(candidate.Role)
                .SelectMany(sensor => bySensor[sensor.Index])
                .Where(r => (candidate.Type is null || r.Type == candidate.Type) && candidate.Label.IsMatch(r.Label))
                .Where(r => Units.Fits(rule.Quantity, r.Unit))
                .Select(r => Bind(r, sensors));

            var found = rule.Aggregate == Aggregate.Max ? matches.ToList() : matches.Take(1).ToList();
            if (found.Count > 0) return found;
        }
        return [];
    }

    private static ResolvedReading? FindOverride(HwinfoSnapshot snapshot, ReadingSelector selector)
    {
        var sensor = snapshot.Sensors.FirstOrDefault(s => s.Name == selector.SensorName && s.Instance == selector.SensorInstance);
        if (sensor is null) return null;
        var reading = snapshot.Readings.FirstOrDefault(r => r.SensorIndex == sensor.Index && r.Label == selector.ReadingLabel);
        return reading is null ? null : Bind(reading, snapshot.Sensors);
    }

    private static ResolvedReading Bind(HwinfoReading reading, IReadOnlyList<HwinfoSensor> sensors) =>
        new(reading.Index, reading.Id, sensors[reading.SensorIndex], reading.Label, reading.Unit);
}
