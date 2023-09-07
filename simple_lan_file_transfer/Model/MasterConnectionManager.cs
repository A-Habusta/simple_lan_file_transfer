using System.Runtime.InteropServices;

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
        SetSocketBufferSizes(socket);

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
        SetSocketBufferSizes(socket);

        await socket.ConnectAsync(ipAddress, port, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            socket.Close();
            throw new OperationCanceledException();
        }

        return socket;
    }

    private static void SetSocketBufferSizes(Socket socket)
    {
        socket.SendBufferSize = Utility.BlockSize;
        socket.ReceiveBufferSize = Utility.BlockSize;
    }
}

public sealed class MasterConnectionManagerListenerWrapper : NetworkLoopBase
{
    public event EventHandler<Socket>? NewIncomingConnection;
    private readonly MasterConnectionManager _masterConnectionManager;

    public MasterConnectionManagerListenerWrapper(MasterConnectionManager masterConnectionManager)
    {
        _masterConnectionManager = masterConnectionManager;
    }

    protected override async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket socket = await _masterConnectionManager.StartNewIncomingTransferAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            NewIncomingConnection?.Invoke(this, socket);
        }
    }
}