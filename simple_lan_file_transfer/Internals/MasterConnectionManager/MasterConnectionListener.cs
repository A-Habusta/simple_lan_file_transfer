namespace simple_lan_file_transfer.Internals;

public class MasterConnectionListener : NetworkLoopBase
{
    public delegate void NewConnectionHandler(Socket socket, CancellationToken cancellationToken = default);
    
    private readonly TcpListener _listener = new(IPAddress.Any, Utility.DefaultPort);
    private readonly NewConnectionHandler _newConnectionHandler;
    
    public MasterConnectionListener(NewConnectionHandler newConnectionHandler)
    {
        _newConnectionHandler = newConnectionHandler;
    }


    protected override async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket socket = await _listener.AcceptSocketAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            
            _newConnectionHandler(socket, cancellationToken);
        }
    }
}