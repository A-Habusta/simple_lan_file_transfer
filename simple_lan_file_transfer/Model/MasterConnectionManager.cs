namespace simple_lan_file_transfer.Models;
public sealed class MasterConnectionManager
{
    private readonly TcpListener _requestListener;
    public MasterConnectionManager(int port)
    {
        _requestListener = new TcpListener(IPAddress.Any, port);
        _requestListener.Start();
    }

    public async Task<Socket> StartNewIncomingTransferAsync (CancellationToken cancellationToken = default)
    {
        Socket socket = await _requestListener.AcceptSocketAsync(cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            socket.Close();
            throw new OperationCanceledException();
        }

        return socket;
    }

    public static async Task<Socket> StartNewOutgoingTransferAsync(IPAddress ipAddress, int port, 
        CancellationToken cancellationToken = default)
    {
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(ipAddress, port, cancellationToken);
        
        if (cancellationToken.IsCancellationRequested)
        {
            socket.Close();
            throw new OperationCanceledException();
        }
        
        return socket;
    }
}