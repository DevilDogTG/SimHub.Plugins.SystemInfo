using System;
using System.Collections.Generic;
using DevDogs.SystemInfo.Hardware;
using DevDogs.SystemInfo.Hwinfo;
using DevDogs.SystemInfo.Matching;

namespace DevDogs.SystemInfo.Publishing;

/// <summary>
/// Turns HWiNFO's shared memory into the published property values. <see cref="Tick"/> runs on one background thread;
/// <see cref="Values"/> can be read from any thread (it is replaced, never mutated).
/// </summary>
public sealed class SystemInfoMonitor : IDisposable
{
    private static readonly IReadOnlyDictionary<string, double?> NoValues = new Dictionary<string, double?>();

    private readonly HwinfoConnection _connection;
    private readonly IReadOnlyList<PropertyRule> _rules;
    private readonly Func<IReadOnlyDictionary<string, ReadingSelector>> _overrides;
    private LiveReader? _reader;
    private long? _lastPollTime;
    private bool _connected;
    private volatile bool _invalidated;
    private volatile Resolution? _resolution;
    private volatile Dictionary<string, object?> _values;
    private volatile StaticHardware _hardware = StaticHardware.None;

    public SystemInfoMonitor(
        ISharedMemoryOpener opener,
        Func<IReadOnlyDictionary<string, ReadingSelector>> overrides,
        IReadOnlyList<PropertyRule>? rules = null)
        : this(new HwinfoConnection(opener), overrides, rules) { }

    internal SystemInfoMonitor(
        HwinfoConnection connection,
        Func<IReadOnlyDictionary<string, ReadingSelector>> overrides,
        IReadOnlyList<PropertyRule>? rules = null)
    {
        _connection = connection;
        _overrides = overrides;
        _rules = rules ?? BuiltInRules.All;
        _values = PublishedProperties.Compute(null, NoValues, _hardware);
    }

    /// <summary>Latest value per <see cref="PublishedProperties.Keys"/> entry.</summary>
    public IReadOnlyDictionary<string, object?> Values => _values;

    /// <summary>Current bindings (for the settings page); <c>null</c> while disconnected.</summary>
    public Resolution? Resolution => _resolution;

    public HwinfoHeader? Header => _connection.Header;

    /// <summary>Static facts from Windows; can be set after start because probing them is slow.</summary>
    public StaticHardware Hardware
    {
        get => _hardware;
        set
        {
            _hardware = value;
            _lastPollTime = null; // recompute derived values on the next tick
        }
    }

    /// <summary>Raised on the tick thread when HWiNFO connects or disconnects.</summary>
    public event Action<bool>? ConnectionChanged;

    /// <summary>Re-resolve on the next tick, e.g. after the user changed an override.</summary>
    public void Invalidate() => _invalidated = true;

    public void Tick()
    {
        if (!_connection.Poll() || _connection.Memory is not { } memory)
        {
            Disconnect();
            return;
        }

        var header = _connection.Header!.Value;
        if (_reader is null || _invalidated || !_reader.Fits(header))
        {
            if (!Resolve(memory, header)) return;
        }
        // HWiNFO stamps each update; between updates the values can't have changed.
        else if (header.PollTime == _lastPollTime)
        {
            return;
        }

        var values = _reader!.Read(memory);
        if (values is null)
        {
            // Readings moved (HWiNFO restarted or added a sensor with the same counts): bind again once.
            if (!Resolve(memory, header) || (values = _reader!.Read(memory)) is null) return;
        }

        _lastPollTime = header.PollTime;
        _values = PublishedProperties.Compute(_resolution, values, _hardware);
        if (_connected) return;
        _connected = true;
        ConnectionChanged?.Invoke(true);
    }

    private bool Resolve(ISharedMemory memory, HwinfoHeader header)
    {
        _invalidated = false;
        var snapshot = HwinfoSnapshot.Read(memory, header);
        if (snapshot is null) return false; // HWiNFO grew the block mid-read; next tick reopens or retries

        _resolution = SensorResolver.Resolve(snapshot, _rules, _overrides());
        _reader = new LiveReader(_resolution, header);
        return true;
    }

    private void Disconnect()
    {
        _reader = null;
        _resolution = null;
        _lastPollTime = null;
        if (!_connected) return;
        _connected = false;
        _values = PublishedProperties.Compute(null, NoValues, _hardware);
        ConnectionChanged?.Invoke(false);
    }

    public void Dispose() => _connection.Dispose();
}
