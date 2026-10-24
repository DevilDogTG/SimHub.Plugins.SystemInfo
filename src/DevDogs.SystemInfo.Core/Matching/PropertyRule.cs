using System.Collections.Generic;
using System.Text.RegularExpressions;
using DevDogs.SystemInfo.Hwinfo;

namespace DevDogs.SystemInfo.Matching;

/// <summary>How several matching readings become one value.</summary>
public enum Aggregate
{
    /// <summary>The first matching reading.</summary>
    First,

    /// <summary>The highest of all readings matching the first candidate that matches anything.</summary>
    Max,
}

/// <summary>One way to find a reading: in which sensors, of which type, with which original label.</summary>
public sealed record ReadingCandidate(SensorRole Role, ReadingType? Type, Regex Label)
{
    public ReadingCandidate(SensorRole role, ReadingType? type, string labelPattern)
        : this(role, type, new Regex(labelPattern, RegexOptions.CultureInvariant)) { }
}

/// <summary>A published property and its candidates, tried in order until one matches.</summary>
public sealed record PropertyRule(string Key, Quantity Quantity, Aggregate Aggregate, IReadOnlyList<ReadingCandidate> Candidates);
