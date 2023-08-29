namespace simple_lan_file_transfer.Internals;

public class MasterConnectionManager
{
    private readonly TcpListener _listener;
    private List<SingleConnectionManager> _connections = new();
    
    private CancellationTokenSource? _broadcastIpAddressCancellationTokenSource;
    private CancellationTokenSource? _readIpAddressBroadcastCancellationTokenSource;
    private UdpClient _broadcastClient;
    private UdpClient _broadcastListener;
    
    private List<IPAddress> _availableIpAddresses = new();
    
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
        
        _listener.Stop();
        foreach (SingleConnectionManager connection in _connections)
        {
            connection.Stop();
        }
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
    
    private void BroadcastIpAddressCancellationTokenSourceDispose()
    {
        _broadcastIpAddressCancellationTokenSource?.Dispose();
        _broadcastIpAddressCancellationTokenSource = null;
    }
    
    private async void BroadcastIpAddress(CancellationToken cancellationToken)
    {
        IPAddress localIpAddress = ((IPEndPoint) _listener.LocalEndpoint).Address;
        var ipAddressBytes = localIpAddress.MapToIPv4().GetAddressBytes();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            await _broadcastClient.SendAsync(ipAddressBytes, cancellationToken);
            await Task.Delay(Utility.BroadcastIntervalMs, cancellationToken);
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