using System;
using System.Collections.Generic;
using System.Threading;
using DevDogs.SystemInfo.Hwinfo;
using DevDogs.SystemInfo.Matching;
using DevDogs.SystemInfo.Publishing;
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

    private static readonly IReadOnlyDictionary<string, ReadingSelector> NoOverrides = new Dictionary<string, ReadingSelector>();

    private SystemInfoMonitor? _monitor;
    private Timer? _timer;
    private bool _hardwareProbed;
    private int _polling;

    public PluginManager PluginManager { get; set; } = null!;

    public void Init(PluginManager pluginManager)
    {
        var monitor = new SystemInfoMonitor(new MemoryMappedHwinfoOpener(), () => NoOverrides);
        monitor.ConnectionChanged += OnConnectionChanged;
        _monitor = monitor;

        foreach (var key in PublishedProperties.Keys)
            this.AttachDelegate(key, () => monitor.Values[key]);

        // Reads run off SimHub's data loop so a slow or missing HWiNFO never stalls it.
        _timer = new Timer(_ => Poll(), null, TimeSpan.Zero, ReadInterval);
    }

    public void End(PluginManager pluginManager)
    {
        using var stopped = new ManualResetEvent(false);
        if (_timer?.Dispose(stopped) == true)
            stopped.WaitOne(TimeSpan.FromSeconds(2));
        _timer = null;
        _monitor?.Dispose();
        _monitor = null;
    }

    private void Poll()
    {
        // Timer callbacks can overlap if a read stalls; skip instead of queueing up.
        if (Interlocked.Exchange(ref _polling, 1) == 1) return;
        try
        {
            var monitor = _monitor;
            if (monitor is null) return;

            if (!_hardwareProbed)
            {
                // WMI takes a moment; doing it here keeps SimHub's startup fast.
                _hardwareProbed = true;
                monitor.Hardware = StaticHardwareProbe.Probe();
            }

            monitor.Tick();
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

    private void OnConnectionChanged(bool connected)
    {
        var monitor = _monitor;
        if (!connected || monitor?.Header is not { } h || monitor.Resolution is not { } resolution)
        {
            SimHub.Logging.Current.Info("DevDogs System Info: HWiNFO shared memory not available, retrying");
            return;
        }

        var unresolved = new List<string>();
        foreach (var property in resolution.Properties.Values)
            if (!property.IsResolved) unresolved.Add(property.Rule.Key);

        SimHub.Logging.Current.Info(
            $"DevDogs System Info: HWiNFO shared memory v{h.Version}.{h.Revision} connected " +
            $"({h.SensorCount} sensors, {h.ReadingCount} readings, polling {h.PollingPeriodMs?.ToString() ?? "?"} ms); " +
            $"CPU '{resolution.CpuModel}', GPU '{resolution.GpuModel}', " +
            (unresolved.Count == 0 ? "all properties bound" : $"not found: {string.Join(", ", unresolved)}"));
    }
}
