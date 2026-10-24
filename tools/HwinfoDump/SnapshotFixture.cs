using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DevDogs.SystemInfo.Hwinfo;

namespace DevDogs.SystemInfo.Tools;

/// <summary>
/// JSON form of an <see cref="HwinfoSnapshot"/>, used as a test fixture. Shared by the dump tool and the tests (linked file).
/// </summary>
public static partial class SnapshotFixture
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(HwinfoSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);

    public static HwinfoSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize<HwinfoSnapshot>(json, Options) ?? throw new InvalidDataException("Empty fixture.");

    /// <summary>
    /// Fixtures go into a public repo: drop drive serial numbers (<c>Drive: EN705 1TB (AA251930030) [C:]</c>) and
    /// user renames, which can name personal things. Original names are what the plugin matches on, so tests lose nothing.
    /// </summary>
    public static HwinfoSnapshot Redact(HwinfoSnapshot snapshot) => snapshot with
    {
        Sensors = snapshot.Sensors.Select(s => s with { Name = SerialNumber().Replace(s.Name, "(SERIAL)"), UserName = "" }).ToList(),
        Readings = snapshot.Readings.Select(r => r with { UserLabel = "" }).ToList(),
    };

    [GeneratedRegex(@"\([A-Z0-9]{10,}\)")]
    private static partial Regex SerialNumber();
}
