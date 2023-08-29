namespace simple_lan_file_transfer.Internals;

public class MasterConnectionManager
{
    private readonly TcpListener _listener;
    private readonly List<SingleConnectionManager> _connections = new();

    private CancellationTokenSource? _broadcastIpAddressCancellationTokenSource;
    private CancellationTokenSource? _listenForIpAddressBroadcastCancellationTokenSource;
    private readonly UdpClient _broadcastClient;
    private readonly UdpClient _broadcastListener;

    private readonly List<IPAddress> _availableIpAddresses = new();

    public MasterConnectionManager()
    {
        _listener = new TcpListener(IPAddress.Any, Utility.DefaultPort);
        _listener.Start();

        _broadcastClient = new UdpClient();
        _broadcastClient.EnableBroadcast = true;
        _broadcastClient.Connect(IPAddress.Broadcast, Utility.DefaultBroadcastPort);

        _broadcastListener = new UdpClient(Utility.DefaultBroadcastPort);

        Task.Run(AcceptConnectionLoop);
    }

    public void Stop()
    {
        StopBroadcastingIpAddress();
        StopListeningForIpAddressBroadcast();

        _listener.Stop();
        _connections.ForEach(connection => connection.Stop());
    }

    public async void ConnectTo(IPAddress ipAddress)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(ipAddress, Utility.DefaultPort);

        _connections.Add(new SingleConnectionManager(socket));
    }

    public void StartBroadcastingIpAddress()
    {
        _broadcastIpAddressCancellationTokenSource = new CancellationTokenSource();
        Task.Run(() => BroadcastIpAddress(_broadcastIpAddressCancellationTokenSource.Token))
            .ContinueWith(_ => BroadcastIpAddressCancellationTokenSourceDispose());
    }

    public void StopBroadcastingIpAddress()
    {
        _broadcastIpAddressCancellationTokenSource?.Cancel();
    }


    public void StartListeningForIpAddressBroadcast()
    {
        _listenForIpAddressBroadcastCancellationTokenSource = new CancellationTokenSource();
        _availableIpAddresses.Clear();
        
        Task.Run(() => ListenForIpAddressBroadcast(_listenForIpAddressBroadcastCancellationTokenSource.Token))
            .ContinueWith(_ => ReadIpAddressBroadcastCancellationTokenSourceDispose());
    }

    public void StopListeningForIpAddressBroadcast()
    {
        _listenForIpAddressBroadcastCancellationTokenSource?.Cancel();
    }


    private void BroadcastIpAddressCancellationTokenSourceDispose()
    {
        _broadcastIpAddressCancellationTokenSource?.Dispose();
        _broadcastIpAddressCancellationTokenSource = null;
    }
    
    private void ReadIpAddressBroadcastCancellationTokenSourceDispose()
    {
        _listenForIpAddressBroadcastCancellationTokenSource?.Dispose();
        _listenForIpAddressBroadcastCancellationTokenSource = null;
    }

    private async void BroadcastIpAddress(CancellationToken cancellationToken)
    {
        IPAddress localIpAddress = ((IPEndPoint) _listener.LocalEndpoint).Address;
        var ipAddressBytes = localIpAddress.GetAddressBytes();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            await _broadcastClient.SendAsync(ipAddressBytes, cancellationToken);
            await Task.Delay(Utility.BroadcastIntervalMs, cancellationToken);
        }
    }
    
    private async void ListenForIpAddressBroadcast(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result = await _broadcastListener.ReceiveAsync(cancellationToken);
            var ipAddress = new IPAddress(result.Buffer);
            _availableIpAddresses.Add(ipAddress);
        }
    }
    
    private async void AcceptConnectionLoop()
    {
        while (true)
        {
            Socket socket = await _listener.AcceptSocketAsync(); 
            _connections.Add(new SingleConnectionManager(socket)); 
        }
    }
}