using System;

namespace DevDogs.SystemInfo.Hwinfo;

/// <summary>
/// Header of HWiNFO's sensor shared memory (<c>HWiNFO_SENS_SM2</c>, packed, little-endian).
/// It locates the sensor and reading arrays that follow it.
/// </summary>
public readonly record struct HwinfoHeader(
    uint Signature,
    uint Version,
    uint Revision,
    long PollTime,
    uint SensorSectionOffset,
    uint SensorElementSize,
    uint SensorCount,
    uint ReadingSectionOffset,
    uint ReadingElementSize,
    uint ReadingCount,
    uint? PollingPeriodMs)
{
    /// <summary>"HWiS": HWiNFO is running and publishing.</summary>
    public const uint ActiveSignature = 0x53695748;

    /// <summary>"DEAD": HWiNFO closed shared memory but a reader still holds the mapping open.</summary>
    public const uint DeadSignature = 0x44414544;

    /// <summary>Classic header, up to the reading count.</summary>
    public const int MinSize = 44;

    /// <summary>Newer HWiNFO (seen on v2 rev 1) appends the polling period; the sensor section then starts at 48.</summary>
    public const int MaxSize = 48;

    public bool IsActive => Signature == ActiveSignature;

    /// <summary>Bytes from the start of the mapping to the end of the last reading.</summary>
    public long TotalSize => ReadingSectionOffset + (long)ReadingElementSize * ReadingCount;

    /// <param name="buffer">Start of the mapping.</param>
    /// <param name="length">Valid bytes in <paramref name="buffer"/>; at least <see cref="MinSize"/>.</param>
    public static HwinfoHeader Parse(byte[] buffer, int length)
    {
        if (length < MinSize || buffer.Length < length)
            throw new ArgumentException($"Header needs {MinSize} bytes, got {length}.", nameof(length));

        var sensorSectionOffset = BitConverter.ToUInt32(buffer, 20);
        // The polling period only exists when the header is long enough to hold it before the sensors begin.
        uint? pollingPeriod = length >= MaxSize && sensorSectionOffset >= MaxSize ? BitConverter.ToUInt32(buffer, 44) : null;

        return new HwinfoHeader(
            Signature: BitConverter.ToUInt32(buffer, 0),
            Version: BitConverter.ToUInt32(buffer, 4),
            Revision: BitConverter.ToUInt32(buffer, 8),
            PollTime: BitConverter.ToInt64(buffer, 12),
            SensorSectionOffset: sensorSectionOffset,
            SensorElementSize: BitConverter.ToUInt32(buffer, 24),
            SensorCount: BitConverter.ToUInt32(buffer, 28),
            ReadingSectionOffset: BitConverter.ToUInt32(buffer, 32),
            ReadingElementSize: BitConverter.ToUInt32(buffer, 36),
            ReadingCount: BitConverter.ToUInt32(buffer, 40),
            PollingPeriodMs: pollingPeriod);
    }
}
