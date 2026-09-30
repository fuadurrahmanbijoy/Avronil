using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Threading;

namespace Anjana.Services;

public sealed record NetSnapshot(
    double DownBps, double UpBps, long TotalDown, long TotalUp,
    string Adapter, bool Connected, bool IsWireless);

/// <summary>Samples interface byte counters once per second and derives smoothed throughput.</summary>
public sealed class NetworkMonitor : IDisposable
{
    public const int HistoryLength = 60;
    private static readonly string[] VirtualHints =
        { "virtual", "vmware", "hyper-v", "virtualbox", "vethernet", "loopback", "pseudo", "bluetooth", "wi-fi direct" };

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _clock = new();
    private readonly Dictionary<string, (long Rx, long Tx)> _last = new();
    private readonly Queue<double> _down = new(), _up = new();
    private List<NetworkInterface> _nics = new();
    private volatile bool _dirty = true;
    private int _ticks;
    private double _smDown, _smUp;
    private long _totDown, _totUp;
    private string _adapter = "No connection";
    private bool _connected, _wireless;

    public NetSnapshot Current { get; private set; } = new(0, 0, 0, 0, "No connection", false, false);
    public event Action<NetSnapshot>? Updated;

    public IReadOnlyList<double> DownHistory => _down.ToArray();
    public IReadOnlyList<double> UpHistory => _up.ToArray();

    public NetworkMonitor()
    {
        _timer.Tick += (_, _) => Tick();
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
    }

    private void OnNetworkChanged(object? s, EventArgs e) => _dirty = true;

    public void Start()
    {
        RefreshInterfaces();
        for (int i = 0; i < HistoryLength; i++) { _down.Enqueue(0); _up.Enqueue(0); }
        _clock.Restart();
        _timer.Start();
        Tick();
    }

    private void RefreshInterfaces()
    {
        _dirty = false;
        try { _nics = NetworkInterface.GetAllNetworkInterfaces().Where(IsCandidate).ToList(); }
        catch { _nics = new(); }

        var ids = _nics.Select(n => n.Id).ToHashSet();
        foreach (var stale in _last.Keys.Where(k => !ids.Contains(k)).ToList()) _last.Remove(stale);

        var primary = _nics.FirstOrDefault(HasGateway);
        _connected = primary is not null;
        var shown = primary ?? _nics.FirstOrDefault();
        _adapter = shown?.Name ?? "No connection";
        _wireless = shown?.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
    }

    private static bool IsCandidate(NetworkInterface n) =>
        n.OperationalStatus == OperationalStatus.Up &&
        n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) &&
        !VirtualHints.Any(h => n.Description.Contains(h, StringComparison.OrdinalIgnoreCase));

    private static bool HasGateway(NetworkInterface n)
    {
        try
        {
            return n.GetIPProperties().GatewayAddresses
                .Any(g => !g.Address.Equals(IPAddress.Any) && !g.Address.Equals(IPAddress.IPv6Any));
        }
        catch { return false; }
    }

    private void Tick()
    {
        if (_dirty || ++_ticks % 15 == 0) RefreshInterfaces();

        double dt = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        if (dt <= 0.05) dt = 1;

        long dRx = 0, dTx = 0;
        foreach (var nic in _nics)
        {
            try
            {
                var st = nic.GetIPStatistics();
                long rx = st.BytesReceived, tx = st.BytesSent;
                if (_last.TryGetValue(nic.Id, out var p))
                {
                    if (rx >= p.Rx) dRx += rx - p.Rx;   // counter reset → ignore that sample
                    if (tx >= p.Tx) dTx += tx - p.Tx;
                }
                _last[nic.Id] = (rx, tx);
            }
            catch (NetworkInformationException) { _dirty = true; }
        }

        _totDown += dRx; _totUp += dTx;
        _smDown = _smDown * 0.4 + (dRx / dt) * 0.6;   // light EMA: responsive but not jittery
        _smUp   = _smUp   * 0.4 + (dTx / dt) * 0.6;
        if (_smDown < 0.5) _smDown = 0;
        if (_smUp < 0.5) _smUp = 0;

        Push(_down, _smDown); Push(_up, _smUp);

        Current = new NetSnapshot(_smDown, _smUp, _totDown, _totUp, _adapter, _connected, _wireless);
        Updated?.Invoke(Current);
    }

    private static void Push(Queue<double> q, double v)
    {
        q.Enqueue(v);
        while (q.Count > HistoryLength) q.Dequeue();
    }

    public void Dispose()
    {
        _timer.Stop();
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
    }
}
