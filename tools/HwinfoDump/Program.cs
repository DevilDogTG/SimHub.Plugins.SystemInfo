using DevDogs.SystemInfo.Hwinfo;
using DevDogs.SystemInfo.Matching;
using DevDogs.SystemInfo.Tools;

// Writes the live HWiNFO sensors as a redacted JSON fixture, so hardware we don't own can be added to the tests.
//   HwinfoDump [output.json]   write a fixture
//   HwinfoDump --resolve       print what each plugin property binds to on this PC, with current values

var resolveOnly = args.Contains("--resolve");
var output = args.FirstOrDefault(a => !a.StartsWith("--")) ?? $"hwinfo-{Environment.MachineName.ToLowerInvariant()}.json";

using var connection = new HwinfoConnection(new MemoryMappedHwinfoOpener());
if (!connection.Poll())
{
    Console.Error.WriteLine("HWiNFO shared memory not found. Start HWiNFO64 with Settings > General > Shared Memory Support enabled.");
    return 1;
}

using var memory = new MemoryMappedHwinfoOpener().TryOpen()!;
var snapshot = HwinfoSnapshot.Read(memory, connection.Header!.Value);
if (snapshot is null)
{
    Console.Error.WriteLine("HWiNFO changed its sensor list while reading; run again.");
    return 1;
}

if (resolveOnly)
{
    var resolution = SensorResolver.Resolve(snapshot, BuiltInRules.All);
    Console.WriteLine($"CPU.Name  {resolution.CpuModel ?? "-"}");
    Console.WriteLine($"GPU.Name  {resolution.GpuModel ?? "-"}");
    foreach (var (key, property) in resolution.Properties)
    {
        var value = property.Evaluate(i => snapshot.Readings[i].Value);
        var source = property.Readings.Count switch
        {
            0 => "NOT FOUND",
            1 => $"{property.Readings[0].Sensor.Name} / {property.Readings[0].Label}",
            var n => $"{property.Readings[0].Sensor.Name} / max of {n}: {string.Join(", ", property.Readings.Select(r => r.Label))}",
        };
        Console.WriteLine($"{key,-22} {value?.ToString("0.##") ?? "-",10}  {source}");
    }
    return resolution.Properties.Values.All(p => p.IsResolved) ? 0 : 2;
}

File.WriteAllText(output, SnapshotFixture.Serialize(SnapshotFixture.Redact(snapshot)));
Console.WriteLine($"Wrote {snapshot.Sensors.Count} sensors, {snapshot.Readings.Count} readings to {Path.GetFullPath(output)}");
return 0;
