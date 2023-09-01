namespace simple_lan_file_transfer.Internals;

public class RequestListener : NetworkLoopBase
{
    public delegate Task NewConnectionHandler(Socket socket, CancellationToken cancellationToken = default);
    
    private readonly TcpListener _listener = new(IPAddress.Any, Utility.DefaultPort);
    private readonly NewConnectionHandler _newConnectionHandler;
    
    public RequestListener(NewConnectionHandler newConnectionHandler)
    {
        _newConnectionHandler = newConnectionHandler;
        
        _listener.Start();
    }

    protected override async Task LoopAsync(CancellationToken cancellationToken)
    {
        for (;;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            Socket socket = await _listener.AcceptSocketAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                socket.Dispose();
                throw new OperationCanceledException(cancellationToken);
            }
            
            await _newConnectionHandler(socket, cancellationToken);
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (Disposed) return;
        
        if (disposing)
        {
            _listener.Stop();
        }
        
        base.Dispose(disposing);
    }
}