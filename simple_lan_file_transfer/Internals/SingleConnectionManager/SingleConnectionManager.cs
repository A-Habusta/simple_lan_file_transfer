using System.IO;

namespace simple_lan_file_transfer.Internals;

public class SingleConnectionManager :
    ISelfDeletingObject<SingleConnectionManager>,
    ISelfDeletingObjectParent<SenderTransferManager>,
    ISelfDeletingObjectParent<ReceiverTransferManager>
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
        
        public byte[] ToBytes()
        {
            var bytes = new byte[Size];
            bytes[0] = (byte)Type;
            BitConverter.TryWriteBytes(bytes.AsSpan(1, 2), Data);
            return bytes;
        }
        
        public static Message FromBytes(byte[] bytes)
        {
            return new Message
            {
                Type = (MessageType)bytes[0],
                Data = BitConverter.ToUInt16(bytes.AsSpan(1, 2))
            };
        }
    }

    private readonly Socket _socket;
    private readonly RequestListener _requestListener;
    
    private readonly List<SenderTransferManager> _outgoingTransfers = new();
    private readonly List<ReceiverTransferManager> _incomingTransfers = new();
    
    public string RootDirectory { get; set; } = string.Empty;

    public ISelfDeletingObjectParent<SingleConnectionManager> Parent { get; }

    public SingleConnectionManager(Socket socket, ISelfDeletingObjectParent<SingleConnectionManager> parent)
    {
        _socket = socket;
        _requestListener = new RequestListener(socket, HandleIncomingTransferRequestAsync, Message.Size);
        
        _requestListener.Run();

        Parent = parent;
    }

    public async Task StartNewTransferAsync(string fileName, CancellationToken cancellationToken = default)
    {
        _requestListener.Stop();
        
        await SendMessageAsync(new Message
        {
            Type = MessageType.RequestTransfer
        }, cancellationToken);

        Message response = await ReceiveMessageAsync(cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            _requestListener.Run();
            return;
        }
        
        if (response.Type != MessageType.SendOpenedPort)
        {
            throw new IOException("Received invalid message type");
        }

        IPAddress remoteIpAddress = ((IPEndPoint)_socket.RemoteEndPoint!).Address;

        var remotePort = response.Data;

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(remoteIpAddress, remotePort, cancellationToken);
        
        if (cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            _requestListener.Run();
            return;
        }

        var transfer = new SenderTransferManager(socket, RootDirectory, this, fileName);
            
        lock (_outgoingTransfers) _outgoingTransfers.Add(transfer);
        
        // Transfers remove themselves from the list when they are stopped, so we don't need to worry about that here
        if (cancellationToken.IsCancellationRequested)
        {
            transfer.CloseConnectionAndDelete();
        }
        
        _requestListener.Run();
    }

    private async Task HandleIncomingTransferRequestAsync(byte[] incomingData, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return;
         
        Message message = Message.FromBytes(incomingData);
        if (message.Type != MessageType.RequestTransfer)
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
        if (cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            return;
        }

        var transfer = new ReceiverTransferManager(socket, RootDirectory, this);
        
        lock (_incomingTransfers) _incomingTransfers.Add(transfer);
        
        if (cancellationToken.IsCancellationRequested)
        {
            transfer.CloseConnectionAndDelete();
        }
    }

    public void CloseConnection()
    {
        _requestListener.Stop();
        
        StopAllIncomingTransfers();
        StopAllOutgoingTransfers();
        
        _socket.Dispose();
    }
    
    public void CloseConnectionAndDelete()
    {
        CloseConnection();
        (this as ISelfDeletingObject<SingleConnectionManager>).RemoveSelfFromParent();
    }
    
    public void RemoveChild(SenderTransferManager child)
    {
        lock (_outgoingTransfers) _outgoingTransfers.Remove(child);
    }
    
    public void RemoveChild(ReceiverTransferManager child)
    {
        lock (_incomingTransfers) _incomingTransfers.Remove(child);
    }

    private void StopAllOutgoingTransfers()
    {
        lock (_outgoingTransfers)
        {
            _outgoingTransfers.ForEach(connection => connection.CloseConnection());
            _outgoingTransfers.Clear();
        }  
    }

    private void StopAllIncomingTransfers()
    {
        lock (_incomingTransfers)
        {
            _incomingTransfers.ForEach(connection => connection.CloseConnection());
            _incomingTransfers.Clear();
        }
    }
    
    private async Task SendMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        await _socket.SendAsync(message.ToBytes(), cancellationToken);
    }

    private async Task<Message> ReceiveMessageAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[Message.Size];
        var read = await _socket.ReceiveAsync(buffer, cancellationToken);
        
        if (cancellationToken.IsCancellationRequested) return default;
        
        if (read != Message.Size)
        {
            throw new IOException("Received invalid message size");
        }

        return Message.FromBytes(buffer);
    }
}


