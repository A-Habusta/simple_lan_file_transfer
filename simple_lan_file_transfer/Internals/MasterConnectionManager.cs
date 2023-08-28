namespace simple_lan_file_transfer.Internals;

public class MasterConnectionManager
{
    private readonly TcpListener _listener;
    private List<SingleConnectionManager> _connections = new();
    
    public MasterConnectionManager()
    {
        _listener = new TcpListener(IPAddress.Any, Utility.DefaultPort);
        _listener.Start();
        
        Task.Run(AcceptLoop);
    }
    
    private async void AcceptLoop()
    {
        while (true)
        {
            Socket socket = await _listener.AcceptSocketAsync(); 
            _connections.Add(new SingleConnectionManager(socket)); 
        }
    }

    public void Stop()
    {
        _listener.Stop();
    }
    
    public async void ConnectTo(IPAddress ipAddress)
    {
        Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(ipAddress, Utility.DefaultPort);
        
        _connections.Add(new SingleConnectionManager(socket));
    }
}