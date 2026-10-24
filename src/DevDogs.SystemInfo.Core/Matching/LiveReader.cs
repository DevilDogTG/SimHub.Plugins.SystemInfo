using System;
using System.Collections.Generic;
using DevDogs.SystemInfo.Hwinfo;

namespace DevDogs.SystemInfo.Matching;

/// <summary>
/// Re-reads only the readings a <see cref="Resolution"/> is bound to (a few dozen values instead of the whole block).
/// Each reading's ID is checked first: if HWiNFO reordered its readings, the caller must resolve again.
/// </summary>
public sealed class LiveReader
{
    private readonly Resolution _resolution;
    private readonly HwinfoHeader _header;
    private readonly byte[] _id = new byte[4];
    private readonly byte[] _value = new byte[8];
    private readonly Dictionary<int, double> _byIndex = new();

    public LiveReader(Resolution resolution, HwinfoHeader header)
    {
        _resolution = resolution;
        _header = header;
    }

    /// <summary>Whether this reader still matches HWiNFO's layout described by <paramref name="current"/>.</summary>
    public bool Fits(HwinfoHeader current) =>
        current.SensorCount == _header.SensorCount
        && current.ReadingCount == _header.ReadingCount
        && current.ReadingSectionOffset == _header.ReadingSectionOffset
        && current.ReadingElementSize == _header.ReadingElementSize;

    /// <summary>Reads current values per property key (<c>null</c> = unresolved). Returns <c>null</c> when the layout moved.</summary>
    public IReadOnlyDictionary<string, double?>? Read(ISharedMemory memory)
    {
        _byIndex.Clear();
        foreach (var property in _resolution.Properties.Values)
        {
            foreach (var reading in property.Readings)
            {
                if (_byIndex.ContainsKey(reading.Index)) continue;

                var at = _header.ReadingSectionOffset + (long)reading.Index * _header.ReadingElementSize;
                if (memory.Read(at + HwinfoSnapshot.Layout.ReadingId, _id, 4) != 4
                    || BitConverter.ToUInt32(_id, 0) != reading.Id
                    || memory.Read(at + HwinfoSnapshot.Layout.ReadingValue, _value, 8) != 8)
                    return null;

                _byIndex[reading.Index] = BitConverter.ToDouble(_value, 0);
            }
        }

        var values = new Dictionary<string, double?>(_resolution.Properties.Count);
        foreach (var property in _resolution.Properties)
            values[property.Key] = property.Value.Evaluate(i => _byIndex[i]);
        return values;
    }
}
