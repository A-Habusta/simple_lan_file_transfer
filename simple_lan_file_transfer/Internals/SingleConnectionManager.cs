using System.IO;

namespace simple_lan_file_transfer.Internals;

public class SingleConnectionManager
{
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
    
    private Socket _socket;
    private CancellationTokenSource _acceptRequestsCancellationTokenSource;
    
    private List<SenderTransferManager> _outgoingTransfers = new();
    private List<ReceiverTransferManager> _incomingTransfers = new();
    
    public SingleConnectionManager(Socket socket)
    {
        _socket = socket;
        _acceptRequestsCancellationTokenSource = new CancellationTokenSource();
        
        Task.Run(() => WaitForTransferRequest(_acceptRequestsCancellationTokenSource.Token));
    }
    
    public void CreateNewTransfer()
    {
        // Stop accepting requests
        _acceptRequestsCancellationTokenSource.Cancel();
        
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
        
        
        IPAddress remoteIpAddress = ((IPEndPoint)_socket.RemoteEndPoint).Address;
        
        if (remoteIpAddress == null)
        {
            throw new IOException("Failed to get remote IP address");
        }
        
        var remotePort = response.Data;
        
        // Start accepting requests again
        _acceptRequestsCancellationTokenSource.TryReset();
        Task.Run(() => WaitForTransferRequest(_acceptRequestsCancellationTokenSource.Token));
        
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(remoteIpAddress, remotePort);
        
        _outgoingTransfers.Add(new SenderTransferManager(socket));
    }

    public void Stop()
    {
        _acceptRequestsCancellationTokenSource.Cancel();
        _acceptRequestsCancellationTokenSource.Dispose();

        StopAllIncomingTransfers();
        StopAllOutgoingTransfers();
        
        _socket.Dispose();
    }
    
    private void StopAllOutgoingTransfers()
    {
        foreach (SenderTransferManager transfer in _outgoingTransfers)
        {
            transfer.Stop();
        }
    }
    
    private void StopAllIncomingTransfers()
    {
        foreach (ReceiverTransferManager transfer in _incomingTransfers)
        {
            transfer.Stop();
        }
    }

    private async void WaitForTransferRequest(CancellationToken cancellationToken)
    {
        var buffer = new byte[Message.Size];
        while (true)
        {
            var read = await _socket.ReceiveAsync(buffer, cancellationToken);
            
            if (cancellationToken.IsCancellationRequested) return;
            
            if (read == Message.Size && buffer[0] == (byte) MessageType.RequestTransfer)
            {
                AcceptTransferRequestAsync();
            }
        }
    }

    private async void AcceptTransferRequestAsync()
    {
        TcpListener tcpListener = new(IPAddress.Any, 0);
        tcpListener.Start();
        var localPort = (ushort) ((IPEndPoint) tcpListener.LocalEndpoint).Port;
        
        SendMessage(new Message
        {
            Type = MessageType.AcceptTransfer,
            Data = localPort
        });
        
        Socket socket = await tcpListener.AcceptSocketAsync(CancellationToken.None);
        tcpListener.Stop();
        
        _incomingTransfers.Add(new ReceiverTransferManager(socket));
    }

    private void SendMessage(Message message)
    {
        var buffer = new byte[Message.Size];
        buffer[0] = (byte) message.Type;
        BitConverter.TryWriteBytes(buffer.AsSpan(1), message.Data);
        _socket.Send(buffer);
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


