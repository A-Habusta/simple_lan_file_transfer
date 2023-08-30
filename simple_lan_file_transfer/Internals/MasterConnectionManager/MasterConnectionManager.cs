namespace simple_lan_file_transfer.Internals;

public class MasterConnectionManager
{
    public List<SingleConnectionManager> Connections => _listener.Connections;
    public List<IPAddress> AvailableIps => _ipBroadcastHandler.AvailableIpAddresses;

    private readonly LocalNetworkAvailabilityBroadcastHandler _ipBroadcastHandler = new();
    private readonly MasterConnectionListener _listener = new();
    
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
        
        lock (Connections)
        {
            Connections.ForEach(connection => connection.Stop());
            Connections.Clear();
        }
    }

    public async void ConnectTo(IPAddress ipAddress, CancellationToken cancellationToken = default)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(ipAddress, Utility.DefaultPort, cancellationToken);
        
        if (cancellationToken.IsCancellationRequested) return;
        
        lock (Connections)
        {
            var connectionManager = new SingleConnectionManager(socket);
            Connections.Add(connectionManager);

            if (!cancellationToken.IsCancellationRequested) return;
            
            connectionManager.Stop();
            socket.Dispose();
            Connections.Remove(connectionManager);
        }
    }
}