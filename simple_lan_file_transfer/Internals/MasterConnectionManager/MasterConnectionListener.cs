namespace simple_lan_file_transfer.Internals;

public class MasterConnectionListener : NetworkLoopBase
{
    private readonly TcpListener _listener = new(IPAddress.Any, Utility.DefaultPort);
    public List <SingleConnectionManager> Connections { get; } = new();


    protected override async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket socket = await _listener.AcceptSocketAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            
            lock (Connections)
            {
                var connectionManager = new SingleConnectionManager(socket);
                Connections.Add(connectionManager);
                
                if (cancellationToken.IsCancellationRequested) break;

                connectionManager.Stop();
                Connections.Remove(connectionManager);
            }
        }
    }
}