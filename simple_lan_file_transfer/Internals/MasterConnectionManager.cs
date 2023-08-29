namespace simple_lan_file_transfer.Internals;

public class MasterConnectionManager
{
    private readonly TcpListener _listener;
    private List<SingleConnectionManager> _connections = new();
    
    private CancellationTokenRegistration _broadcastIpAddressCancellationTokenRegistration = new();
    private CancellationTokenRegistration _readIpAddressBroadcastCancellationTokenRegistration = new();
    private UdpClient _broadcastClient;
    
    public MasterConnectionManager()
    {
        _listener = new TcpListener(IPAddress.Any, Utility.DefaultPort);
        _listener.Start();

        _broadcastClient = new UdpClient();
        _broadcastClient.EnableBroadcast = true;
        _broadcastClient.Client.Bind(new IPEndPoint(IPAddress.Any, Utility.DefaultBroadcastPort));
        
        Task.Run(AcceptLoop);
    }

    public void Stop()
    {
        _broadcastIpAddressCancellationTokenRegistration.Dispose();
        _readIpAddressBroadcastCancellationTokenRegistration.Dispose(); 
        
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
    
    public void StartBroadcastingIPAddress()
    {
        Task.Run(() => BroadcastIpAddress(_broadcastIpAddressCancellationTokenRegistration.Token));
    }

    private async void BroadcastIpAddress(CancellationToken cancellationToken)
    {
        IPAddress localIpAddress = ((IPEndPoint) _listener.LocalEndpoint).Address;
        var ipAddressBytes = localIpAddress.MapToIPv4().GetAddressBytes();
        
        while (true)
        {
        }
    }
    
    private async void AcceptLoop()
    {
        while (true)
        {
            Socket socket = await _listener.AcceptSocketAsync(); 
            _connections.Add(new SingleConnectionManager(socket)); 
        }
    }
}