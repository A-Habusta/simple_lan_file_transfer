namespace simple_lan_file_transfer.Internals;

public class RequestListener : NetworkLoopBase
{
    public delegate void NewConnectionHandler(Socket socket);

    private readonly NewConnectionHandler _newConnectionHandler;
    private readonly TcpListener _listener;
    
    public RequestListener(int port, NewConnectionHandler newConnectionHandler)
    {
        _listener = new TcpListener(IPAddress.Any, port);
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
            
            _newConnectionHandler(socket);
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