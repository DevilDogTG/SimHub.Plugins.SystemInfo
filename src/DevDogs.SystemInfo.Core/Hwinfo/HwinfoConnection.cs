using System;
using System.Diagnostics;

namespace DevDogs.SystemInfo.Hwinfo;

/// <summary>
/// Keeps HWiNFO's shared memory open across HWiNFO starts and stops. Call <see cref="Poll"/> from one thread.
/// </summary>
public sealed class HwinfoConnection : IDisposable
{
    /// <summary>How long to wait before trying to open the mapping again after it was missing or dead.</summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    private readonly ISharedMemoryOpener _opener;
    private readonly Func<TimeSpan> _now;
    private readonly byte[] _headerBuffer = new byte[HwinfoHeader.MaxSize];
    private ISharedMemory? _memory;
    private TimeSpan? _nextOpenAttempt;

    public HwinfoConnection(ISharedMemoryOpener opener)
        : this(opener, StopwatchClock()) { }

    internal HwinfoConnection(ISharedMemoryOpener opener, Func<TimeSpan> now)
    {
        _opener = opener;
        _now = now;
    }

    /// <summary>Header from the last successful <see cref="Poll"/>; <c>null</c> while disconnected.</summary>
    public HwinfoHeader? Header { get; private set; }

    public bool IsConnected => Header is not null;

    /// <summary>The open mapping while connected; valid until the next <see cref="Poll"/>.</summary>
    public ISharedMemory? Memory => IsConnected ? _memory : null;

    /// <summary>Opens the mapping if needed and re-reads the header. Returns whether HWiNFO is publishing.</summary>
    public bool Poll()
    {
        if (_memory is null && !TryOpen())
            return false;

        var read = _memory!.Read(0, _headerBuffer, HwinfoHeader.MaxSize);
        if (read >= HwinfoHeader.MinSize)
        {
            var header = HwinfoHeader.Parse(_headerBuffer, read);
            if (header.IsActive)
            {
                Header = header;
                return true;
            }
        }

        // HWiNFO closed (signature DEAD) or the block shrank: drop it so the next open gets a fresh mapping.
        Close();
        _nextOpenAttempt = _now() + RetryInterval;
        return false;
    }

    private bool TryOpen()
    {
        if (_nextOpenAttempt is { } next && _now() < next)
            return false;

        _memory = _opener.TryOpen();
        if (_memory is null)
            _nextOpenAttempt = _now() + RetryInterval;
        return _memory is not null;
    }

    private void Close()
    {
        _memory?.Dispose();
        _memory = null;
        Header = null;
    }

    public void Dispose() => Close();

    private static Func<TimeSpan> StopwatchClock()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed;
    }
}
