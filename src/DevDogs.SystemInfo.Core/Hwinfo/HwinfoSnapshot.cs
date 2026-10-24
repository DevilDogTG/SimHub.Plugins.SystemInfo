using System;
using System.Collections.Generic;
using System.Text;

namespace DevDogs.SystemInfo.Hwinfo;

/// <summary>HWiNFO's reading type (<c>SENSOR_READING_TYPE</c>).</summary>
public enum ReadingType : uint
{
    None = 0,
    Temperature = 1,
    Voltage = 2,
    Fan = 3,
    Current = 4,
    Power = 5,
    Clock = 6,
    Usage = 7,
    Other = 8,
}

/// <param name="Name">Original name HWiNFO generates; renames in HWiNFO's UI only change <paramref name="UserName"/>.</param>
public sealed record HwinfoSensor(int Index, uint Id, uint Instance, string Name, string UserName);

/// <param name="Label">Original label HWiNFO generates; renames only change <paramref name="UserLabel"/>.</param>
public sealed record HwinfoReading(
    int Index, ReadingType Type, int SensorIndex, uint Id, string Label, string UserLabel, string Unit, double Value);

/// <summary>Every sensor and reading HWiNFO publishes at one moment.</summary>
public sealed record HwinfoSnapshot(HwinfoHeader Header, IReadOnlyList<HwinfoSensor> Sensors, IReadOnlyList<HwinfoReading> Readings)
{
    /// <summary>Copies and parses the whole block; meant for (re)resolving, not for every tick.</summary>
    public static HwinfoSnapshot? Read(ISharedMemory memory, HwinfoHeader header)
    {
        var buffer = new byte[header.TotalSize];
        if (memory.Read(0, buffer, buffer.Length) != buffer.Length)
            return null;
        return Parse(header, buffer);
    }

    public static HwinfoSnapshot Parse(HwinfoHeader header, byte[] block)
    {
        var sensors = new HwinfoSensor[header.SensorCount];
        for (var i = 0; i < sensors.Length; i++)
        {
            var at = (int)(header.SensorSectionOffset + i * header.SensorElementSize);
            sensors[i] = new HwinfoSensor(
                Index: i,
                Id: BitConverter.ToUInt32(block, at + Layout.SensorId),
                Instance: BitConverter.ToUInt32(block, at + Layout.SensorInstance),
                Name: Ansi(block, at + Layout.SensorName, Layout.NameLength),
                UserName: header.SensorElementSize >= Layout.SensorUtf8End
                    ? Utf8(block, at + Layout.SensorUserNameUtf8, Layout.NameLength)
                    : Ansi(block, at + Layout.SensorUserName, Layout.NameLength));
        }

        var readings = new HwinfoReading[header.ReadingCount];
        for (var i = 0; i < readings.Length; i++)
        {
            var at = (int)(header.ReadingSectionOffset + i * header.ReadingElementSize);
            var hasUtf8 = header.ReadingElementSize >= Layout.ReadingUtf8End;
            readings[i] = new HwinfoReading(
                Index: i,
                Type: (ReadingType)BitConverter.ToUInt32(block, at + Layout.ReadingType),
                SensorIndex: (int)BitConverter.ToUInt32(block, at + Layout.ReadingSensorIndex),
                Id: BitConverter.ToUInt32(block, at + Layout.ReadingId),
                Label: Ansi(block, at + Layout.ReadingLabel, Layout.NameLength),
                UserLabel: hasUtf8
                    ? Utf8(block, at + Layout.ReadingUserLabelUtf8, Layout.NameLength)
                    : Ansi(block, at + Layout.ReadingUserLabel, Layout.NameLength),
                Unit: hasUtf8
                    ? Utf8(block, at + Layout.ReadingUnitUtf8, Layout.UnitLength)
                    : Ansi(block, at + Layout.ReadingUnit, Layout.UnitLength),
                Value: BitConverter.ToDouble(block, at + Layout.ReadingValue));
        }

        return new HwinfoSnapshot(header, sensors, readings);
    }

    // Original names are only published in the ANSI fields. They are ASCII in practice; Latin-1 decodes any byte
    // without throwing and is available on every runtime without a code-page provider.
    private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

    private static string Ansi(byte[] block, int at, int length) => Latin1.GetString(block, at, CStringLength(block, at, length));

    private static string Utf8(byte[] block, int at, int length) => Encoding.UTF8.GetString(block, at, CStringLength(block, at, length));

    private static int CStringLength(byte[] block, int at, int max)
    {
        var end = Array.IndexOf(block, (byte)0, at, max);
        return (end < 0 ? at + max : end) - at;
    }

    /// <summary>Field offsets inside the sensor and reading elements (HWiNFO SDK, packed).</summary>
    internal static class Layout
    {
        public const int NameLength = 128;
        public const int UnitLength = 16;

        public const int SensorId = 0;
        public const int SensorInstance = 4;
        public const int SensorName = 8;
        public const int SensorUserName = 136;
        public const int SensorUserNameUtf8 = 264;
        public const int SensorUtf8End = 392;

        public const int ReadingType = 0;
        public const int ReadingSensorIndex = 4;
        public const int ReadingId = 8;
        public const int ReadingLabel = 12;
        public const int ReadingUserLabel = 140;
        public const int ReadingUnit = 268;
        public const int ReadingValue = 284;
        public const int ReadingUserLabelUtf8 = 316;
        public const int ReadingUnitUtf8 = 444;
        public const int ReadingUtf8End = 460;
    }
}
