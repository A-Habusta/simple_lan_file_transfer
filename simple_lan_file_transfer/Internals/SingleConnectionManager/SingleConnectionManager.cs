using System.IO;

namespace simple_lan_file_transfer.Internals;

public class SingleConnectionManager
{
    private enum MessageType
    {
        RequestTransfer,
        SendOpenedPort,
    }

    private struct Message
    {
        public const int Size = 3;
        public MessageType Type { get; init; }
        public ushort Data { get; init; }
    }

    private readonly Socket _socket;
    private readonly RequestListener _requestListener;

    private readonly List<SenderTransferManager> _outgoingTransfers = new();
    private readonly List<ReceiverTransferManager> _incomingTransfers = new();

    public SingleConnectionManager(Socket socket)
    {
        _socket = socket;
        _requestListener = new RequestListener(socket, HandleIncomingTransferRequestAsync, Message.Size);
        
        _requestListener.Run();
    }

    public async Task StartNewTransferAsync(CancellationToken cancellationToken = default)
    {
        _requestListener.Stop();
        
        await SendMessageAsync(new Message
        {
            Type = MessageType.RequestTransfer
        }, cancellationToken);

        Message response = await ReceiveMessageAsync(cancellationToken);

        if (response.Type != MessageType.SendOpenedPort)
        {
            throw new IOException("Received invalid message type");
        }

        IPAddress remoteIpAddress = ((IPEndPoint)_socket.RemoteEndPoint!).Address;

        var remotePort = response.Data;

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(remoteIpAddress, remotePort, cancellationToken);

        _outgoingTransfers.Add(new SenderTransferManager(socket));
        
        _requestListener.Run();
    }

    private async Task HandleIncomingTransferRequestAsync(byte[] incomingData, CancellationToken cancellationToken)
    {
        var messageType = (MessageType) incomingData[0];
        if (messageType != MessageType.RequestTransfer)
        {
            throw new IOException("Received unexpected message type");
        }
        
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        
        await SendMessageAsync(new Message
        {
            Type = MessageType.SendOpenedPort,
            Data = (ushort) ((IPEndPoint) socket.LocalEndPoint!).Port
        }, cancellationToken);
        
        await socket.AcceptAsync(cancellationToken);
        
        _incomingTransfers.Add(new ReceiverTransferManager(socket));
    }

    public void Stop()
    {
        _requestListener.Stop();
        
        StopAllIncomingTransfers();
        StopAllOutgoingTransfers();
        
        _socket.Dispose();
    }
    
    private void StopAllOutgoingTransfers() => _outgoingTransfers.ForEach(connection => connection.Stop());
    private void StopAllIncomingTransfers() => _incomingTransfers.ForEach(connection => connection.Stop());
    
    private async Task SendMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[Message.Size];
        buffer[0] = (byte) message.Type;
        BitConverter.TryWriteBytes(buffer.AsSpan(1), message.Data);
        await _socket.SendAsync(buffer, cancellationToken);
    }

    private async Task<Message> ReceiveMessageAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[Message.Size];
        var read = await _socket.ReceiveAsync(buffer, cancellationToken);
        if (read != Message.Size)
        {
            throw new IOException("Received invalid message size");
        }
        
        return new Message
        {
            Type = (MessageType) buffer[0],
            Data = BitConverter.ToUInt16(buffer.AsSpan(1))
        };
    }
}


