using System;

namespace DevDogs.SystemInfo.Matching;

/// <summary>What a property measures; fixes the unit the plugin publishes.</summary>
public enum Quantity
{
    /// <summary>°C</summary>
    Temperature,

    /// <summary>%</summary>
    Percent,

    /// <summary>MHz</summary>
    Clock,

    /// <summary>W</summary>
    Power,

    /// <summary>RPM</summary>
    FanSpeed,

    /// <summary>MB</summary>
    Memory,

    /// <summary>FPS</summary>
    Framerate,
}

public static class Units
{
    /// <summary>Converts a HWiNFO value to the published unit. Units HWiNFO lets users switch (°F, GB, GHz) are handled.</summary>
    public static double Normalize(Quantity quantity, string unit, double value) => (quantity, unit.Trim()) switch
    {
        (Quantity.Temperature, "F" or "°F") => (value - 32) * 5 / 9,
        (Quantity.Clock, "GHz") => value * 1000,
        (Quantity.Memory, "GB") => value * 1024,
        (Quantity.Memory, "KB") => value / 1024,
        _ => value,
    };

    /// <summary>Whether a HWiNFO unit can feed <paramref name="quantity"/>; filters the override picker.</summary>
    public static bool Fits(Quantity quantity, string unit) => quantity switch
    {
        Quantity.Temperature => unit is "C" or "°C" or "F" or "°F",
        Quantity.Percent => unit == "%",
        Quantity.Clock => unit is "MHz" or "GHz",
        Quantity.Power => unit == "W",
        Quantity.FanSpeed => unit == "RPM",
        Quantity.Memory => unit is "MB" or "GB" or "KB",
        Quantity.Framerate => unit == "FPS",
        _ => throw new ArgumentOutOfRangeException(nameof(quantity), quantity, null),
    };
}
