using ReactiveUI;

namespace simple_lan_file_transfer.Internals;

public class RequestListener : NetworkLoopBase
{
    public delegate Task RequestReceivedHandler(byte[] data, CancellationToken cancellationToken);
    
    private readonly Socket _socket;
    private readonly RequestReceivedHandler _requestReceivedHandler;    
    private readonly int _requestSize;

    public RequestListener(Socket socket, RequestReceivedHandler requestReceivedHandler, int requestSize)
    {
        _socket = socket;
        _requestSize = requestSize;
        _requestReceivedHandler = requestReceivedHandler;
    }

    protected override async void Loop(CancellationToken cancellationToken)
    {
        var buffer = new byte[_requestSize];
        while (!cancellationToken.IsCancellationRequested)
        {
            await _socket.ReceiveAsync(buffer, cancellationToken);
            await _requestReceivedHandler(buffer, cancellationToken);
        }
    }
}