namespace simple_lan_file_transfer.Internals;
public sealed class MasterConnectionManager
{
    public string RootDirectory { get; set; } = Utility.DefaultRootDirectory;

    private readonly TcpListener _requestListener;
    public MasterConnectionManager(int port)
    {
        _requestListener = new TcpListener(IPAddress.Any, port);
        _requestListener.Start();
    }

    public async Task<ReceiverTransferManager> StartNewIncomingTransferAsync (CancellationToken cancellationToken = default)
    {
        Socket socket = await _requestListener.AcceptSocketAsync(cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            socket.Close();
            throw new OperationCanceledException();
        }
        
        ReceiverTransferManager transfer = new(socket, RootDirectory);
        return transfer;
    }

    public static async Task<SenderTransferManager> StartNewOutgoingTransferAsync(IPAddress ipAddress, int port,
        FileStream file, CancellationToken cancellationToken = default)
    {
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(ipAddress, port, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            socket.Close();
            throw new OperationCanceledException();
        }

        SenderTransferManager transfer = new(socket, file);
        return transfer;
    }
}