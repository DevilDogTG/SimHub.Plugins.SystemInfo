using DevDogs.SystemInfo.Hwinfo;
using DevDogs.SystemInfo.Tools;

// Writes the live HWiNFO sensors as a redacted JSON fixture, so hardware we don't own can be added to the tests.
//   HwinfoDump [output.json]

var output = args.Length > 0 ? args[0] : $"hwinfo-{Environment.MachineName.ToLowerInvariant()}.json";

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

File.WriteAllText(output, SnapshotFixture.Serialize(SnapshotFixture.Redact(snapshot)));
Console.WriteLine($"Wrote {snapshot.Sensors.Count} sensors, {snapshot.Readings.Count} readings to {Path.GetFullPath(output)}");
return 0;
