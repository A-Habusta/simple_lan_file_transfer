using System.IO;

namespace simple_lan_file_transfer.Internals;

public class SingleConnectionManager
{
    private class RequestListener : NetworkLoopBase
    {
        private readonly Socket _socket;
        
        public RequestListener(Socket socket)
        {
            _socket = socket;
        }
        
        protected override async void Loop(CancellationToken cancellationToken)
        {
            var buffer = new byte[Message.Size];
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await _socket.ReceiveAsync(buffer, cancellationToken);
                if (read != Message.Size)
                {
                    throw new IOException("Received invalid message size");
                }
                
                var message = new Message
                {
                    Type = (MessageType) buffer[0],
                    Data = BitConverter.ToUInt16(buffer.AsSpan(1))
                };
                
                if (message.Type == MessageType.RequestTransfer)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return;
                }
            }
        }
    }
    
    private enum MessageType
    {
        RequestTransfer,
        AcceptTransfer,
        RejectTransfer
    }

    private struct Message
    {
        public const int Size = 3;
        public MessageType Type { get; set; }
        public ushort Data { get; set; }
    }
    
    private readonly Socket _socket;
    private CancellationTokenSource? _acceptRequestsCancellationTokenSource;
    
    private readonly List<SenderTransferManager> _outgoingTransfers = new();
    private readonly List<ReceiverTransferManager> _incomingTransfers = new();
    
    public SingleConnectionManager(Socket socket)
    {
        _socket = socket;
        
        StartWaitForTransferRequest();
    }
    
    public void CreateNewTransfer()
    {
        StopWaitForTransferRequest();
        
        SendMessage(new Message
        {
            Type = MessageType.RequestTransfer
        });
        
        Message response = WaitForMessage();
        
        if (response.Type == MessageType.RejectTransfer)
        {
            throw new IOException("Transfer rejected");
        }
        
        if (response.Type != MessageType.AcceptTransfer)
        {
            throw new IOException("Received invalid message type");
        }
        
        
        IPAddress remoteIpAddress = ((IPEndPoint)_socket.RemoteEndPoint!).Address;
        
        if (remoteIpAddress == null)
        {
            throw new IOException("Failed to get remote IP address");
        }
        
        var remotePort = response.Data;
        
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(remoteIpAddress, remotePort);
        
        StartWaitForTransferRequest();
        
        _outgoingTransfers.Add(new SenderTransferManager(socket));
    }

    public void Stop()
    {
        StopWaitForTransferRequest();

        StopAllIncomingTransfers();
        StopAllOutgoingTransfers();
        
        _socket.Dispose();
    }
    
    private void StartWaitForTransferRequest()
    {
        _acceptRequestsCancellationTokenSource = new CancellationTokenSource();
        Task.Run(() => WaitForTransferRequest(_acceptRequestsCancellationTokenSource.Token))
            .ContinueWith(_ => AcceptRequestsCancellationTokenSourceDispose());
    }
    
    private void StopAllOutgoingTransfers() => _outgoingTransfers.ForEach(connection => connection.Stop());
    private void StopAllIncomingTransfers() => _incomingTransfers.ForEach(connection => connection.Stop());
    
    private void StopWaitForTransferRequest()
    {
        _acceptRequestsCancellationTokenSource?.Cancel();
    }
    
    private void AcceptRequestsCancellationTokenSourceDispose()
    {
        _acceptRequestsCancellationTokenSource?.Dispose();
        _acceptRequestsCancellationTokenSource = null;
    }  


    private async void SendMessage(Message message)
    {
        var buffer = new byte[Message.Size];
        buffer[0] = (byte) message.Type;
        BitConverter.TryWriteBytes(buffer.AsSpan(1), message.Data);
        await _socket.SendAsync(buffer, CancellationToken.None);
    }

    private Message WaitForMessage()
    {
        var buffer = new byte[Message.Size];
        var read = _socket.Receive(buffer);
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


