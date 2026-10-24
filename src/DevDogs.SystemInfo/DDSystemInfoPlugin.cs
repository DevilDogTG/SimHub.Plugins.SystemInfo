using System;
using System.Threading;
using DevDogs.SystemInfo.Hwinfo;
using SimHub.Plugins;

namespace DevDogs.SystemInfo;

/// <summary>
/// Publishes HWiNFO64 sensors as hardware-agnostic SimHub properties (<c>DDSystemInfoPlugin.*</c>, ADR-0002).
/// </summary>
[PluginName("DevDogs System Info")]
[PluginAuthor("DevilDogTG")]
[PluginDescription("HWiNFO64 sensors as hardware-agnostic properties: CPU, GPU, RAM and FPS.")]
public sealed class DDSystemInfoPlugin : IPlugin
{
    /// <summary>HWiNFO polls every 2 s by default; 250 ms keeps up with faster polling without touching shared memory at 60 Hz.</summary>
    private static readonly TimeSpan ReadInterval = TimeSpan.FromMilliseconds(250);

    private HwinfoConnection? _connection;
    private Timer? _timer;
    private volatile bool _connected;
    private int _polling;

    public PluginManager PluginManager { get; set; } = null!;

    public void Init(PluginManager pluginManager)
    {
        _connection = new HwinfoConnection(new MemoryMappedHwinfoOpener());
        this.AttachDelegate("Status.Connected", () => _connected);

        // Reads run off SimHub's data loop so a slow or missing HWiNFO never stalls it.
        _timer = new Timer(_ => Poll(), null, TimeSpan.Zero, ReadInterval);
    }

    public void End(PluginManager pluginManager)
    {
        using var stopped = new ManualResetEvent(false);
        if (_timer?.Dispose(stopped) == true)
            stopped.WaitOne(TimeSpan.FromSeconds(2));
        _timer = null;
        _connection?.Dispose();
        _connection = null;
    }

    private void Poll()
    {
        // Timer callbacks can overlap if a read stalls; skip instead of queueing up.
        if (Interlocked.Exchange(ref _polling, 1) == 1) return;
        try
        {
            var connection = _connection;
            if (connection is null) return;

            var connected = connection.Poll();
            if (connected != _connected)
            {
                _connected = connected;
                var header = connection.Header;
                SimHub.Logging.Current.Info(header is { } h
                    ? $"DevDogs System Info: HWiNFO shared memory v{h.Version}.{h.Revision} connected " +
                      $"({h.SensorCount} sensors, {h.ReadingCount} readings, polling {h.PollingPeriodMs?.ToString() ?? "?"} ms)"
                    : "DevDogs System Info: HWiNFO shared memory not available, retrying");
            }
        }
        catch (Exception ex)
        {
            SimHub.Logging.Current.Error("DevDogs System Info: reading HWiNFO failed", ex);
        }
        finally
        {
            Volatile.Write(ref _polling, 0);
        }
    }
}
