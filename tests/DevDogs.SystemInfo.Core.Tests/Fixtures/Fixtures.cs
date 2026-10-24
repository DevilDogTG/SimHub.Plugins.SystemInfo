using System.Text;
using DevDogs.SystemInfo.Hwinfo;
using DevDogs.SystemInfo.Tools;
using L = DevDogs.SystemInfo.Hwinfo.HwinfoSnapshot.Layout;

namespace DevDogs.SystemInfo.Core.Tests.Fixtures;

internal static class Fixture
{
    /// <summary>Owner's PC, HWiNFO v2 rev 1: MSI PRO B760M-A, Intel Core i5-14600K, Sapphire RX 9070 XT, 4x16 GB DDR4-3200.</summary>
    public static HwinfoSnapshot IntelAmdGpu => Load("i5-14600K_rx9070xt.json");

    public static HwinfoSnapshot Load(string name) =>
        SnapshotFixture.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)));

    /// <summary>Writes a snapshot back into HWiNFO's binary layout, so parser tests run on the real shape.</summary>
    public static byte[] ToBlock(HwinfoSnapshot snapshot)
    {
        var h = snapshot.Header;
        var block = new byte[h.TotalSize];
        void U32(long at, uint v) => BitConverter.GetBytes(v).CopyTo(block, at);
        void Str(long at, string s, Encoding e, int max) => e.GetBytes(s).Take(max - 1).ToArray().CopyTo(block, at);

        U32(0, h.Signature);
        U32(4, h.Version);
        U32(8, h.Revision);
        BitConverter.GetBytes(h.PollTime).CopyTo(block, 12);
        U32(20, h.SensorSectionOffset);
        U32(24, h.SensorElementSize);
        U32(28, h.SensorCount);
        U32(32, h.ReadingSectionOffset);
        U32(36, h.ReadingElementSize);
        U32(40, h.ReadingCount);
        if (h.PollingPeriodMs is { } period) U32(44, period);

        foreach (var s in snapshot.Sensors)
        {
            var at = h.SensorSectionOffset + s.Index * h.SensorElementSize;
            U32(at + L.SensorId, s.Id);
            U32(at + L.SensorInstance, s.Instance);
            Str(at + L.SensorName, s.Name, Encoding.Latin1, L.NameLength);
            Str(at + L.SensorUserName, s.UserName, Encoding.Latin1, L.NameLength);
            Str(at + L.SensorUserNameUtf8, s.UserName, Encoding.UTF8, L.NameLength);
        }

        foreach (var r in snapshot.Readings)
        {
            var at = h.ReadingSectionOffset + r.Index * h.ReadingElementSize;
            U32(at + L.ReadingType, (uint)r.Type);
            U32(at + L.ReadingSensorIndex, (uint)r.SensorIndex);
            U32(at + L.ReadingId, r.Id);
            Str(at + L.ReadingLabel, r.Label, Encoding.Latin1, L.NameLength);
            Str(at + L.ReadingUserLabel, r.UserLabel, Encoding.Latin1, L.NameLength);
            Str(at + L.ReadingUnit, r.Unit, Encoding.Latin1, L.UnitLength);
            BitConverter.GetBytes(r.Value).CopyTo(block, at + L.ReadingValue);
            Str(at + L.ReadingUserLabelUtf8, r.UserLabel, Encoding.UTF8, L.NameLength);
            Str(at + L.ReadingUnitUtf8, r.Unit, Encoding.UTF8, L.UnitLength);
        }

        return block;
    }
}
