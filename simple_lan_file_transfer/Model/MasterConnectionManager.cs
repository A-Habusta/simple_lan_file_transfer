namespace simple_lan_file_transfer.Models;
public sealed class MasterConnectionManager
{
    private readonly TcpListener _requestListener;
    public MasterConnectionManager(int port)
    {
        _requestListener = new TcpListener(IPAddress.Any, port);

        // This should not be done on Windows, reason here: https://stackoverflow.com/a/14388707
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _requestListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

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
        IPEndPoint endPoint = new(IPAddress.Any, 0);
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(endPoint);

        await socket.ConnectAsync(ipAddress, port, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            socket.Close();
            throw new OperationCanceledException();
        }

        return socket;
    }
}