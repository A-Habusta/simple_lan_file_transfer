namespace simple_lan_file_transfer.Internals;

public class MasterConnectionManager : ISelfDeletingObjectParent<SingleConnectionManager>
{
    public List<SingleConnectionManager> Connections { get; } = new();
    public List<IPAddress> AvailableIps => _ipBroadcastHandler.AvailableIpAddresses;

    private readonly LocalNetworkAvailabilityBroadcastHandler _ipBroadcastHandler = new();
    private readonly MasterConnectionListener _listener;
    
    public MasterConnectionManager()
    {
        _listener = new MasterConnectionListener(newConnectionHandler: CreateAndSaveNewChild);
        _listener.Run();
    }
    
    private string _rootDirectory = string.Empty;
    
    public void ChangeRootDirectory(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        
        lock (Connections)
        {
            Connections.ForEach(connection => connection.RootDirectory = rootDirectory);
        }
    }
    
    public void Stop()
    {
        _listener.Stop();
        
        _ipBroadcastHandler.StopBroadcast();
        _ipBroadcastHandler.StopListening();
        
        CloseAllChildConnections();
    }

    public async void ConnectTo(IPAddress ipAddress, CancellationToken cancellationToken = default)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(ipAddress, Utility.DefaultPort, cancellationToken);
        
        if (cancellationToken.IsCancellationRequested) return;
        
        CreateAndSaveNewChild(socket, cancellationToken);
    }
    
    public void RemoveChild(SingleConnectionManager connectionManager)
    {
        lock (Connections)
        {
            Connections.Remove(connectionManager);
        }
    }
    
    public void CloseAllChildConnections()
    {
        lock (Connections)
        {
            Connections.ForEach(connection => connection.CloseConnection());
            Connections.Clear();
        }
    }

    private void CreateAndSaveNewChild(Socket socket, CancellationToken creationCancellationToken = default)
    {
        var connectionManager = new SingleConnectionManager(socket, this);
        
        lock (Connections)
        {
            Connections.Add(connectionManager);
        }
        
        if (creationCancellationToken.IsCancellationRequested)
        {
            connectionManager.CloseConnectionAndDelete();
        }
    }
}