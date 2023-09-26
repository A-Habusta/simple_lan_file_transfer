using System.Runtime.InteropServices;

namespace simple_lan_file_transfer.Models;

/// <summary>
/// Class used for creating new incoming and outgoing connections.
/// </summary>
public sealed class MasterConnectionManager
{
    private readonly TcpListener _requestListener;
    /// <summary>
    /// Creates a new instance of <see cref="MasterConnectionManager"/> and starts listening for incoming connections on
    /// the specified port.
    /// </summary>
    /// <param name="port">Port of the local listen endpoint</param>
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

    /// <summary>
    /// Wait for a remote sender connection and create a new socket for it.
    /// </summary>
    /// <param name="cancellationToken"/>
    /// <returns>New socket connected to a remote sender</returns>
    /// <exception cref="OperationCanceledException">
    /// Throws if the <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
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

    /// <summary>
    ///
    /// </summary>
    /// <param name="ipAddress">Remote host address</param>
    /// <param name="port">Remote host port</param>
    /// <param name="cancellationToken"/>
    /// <returns>Socket connected to the remote endpoint</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the <paramref name="cancellationToken"/> is cancelled
    /// </exception>
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
        socket.SendBufferSize = Utility.SocketBufferSize;
        socket.ReceiveBufferSize = Utility.SocketBufferSize;
    }
}

/// <summary>
/// Wrapper around <see cref="MasterConnectionManager"/> that listens for incoming connections in the background
/// continuously and raises an event when a new connection is received.
/// </summary>
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