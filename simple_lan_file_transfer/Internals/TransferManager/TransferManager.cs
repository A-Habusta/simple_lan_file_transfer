namespace simple_lan_file_transfer.Internals;

public class SenderTransferManager : TransferManagerBase, ISelfDeletingObject<SenderTransferManager>
{
   private string _fileName;

   public SenderTransferManager(
      Socket socket,
      string rootDirectory,
      ISelfDeletingObjectParent<SenderTransferManager> parent,
      string fileName)
      : base(socket, rootDirectory)
   {
      _fileName = fileName;
      Parent = parent;
   }
   
   public ISelfDeletingObjectParent<SenderTransferManager> Parent { get; }

   protected override async Task CommunicateTransferParametersAsync(CancellationToken cancellationToken = default)
   {
      
   }
   
   protected override async Task HandleFileTransferAsync(CancellationToken cancellationToken = default)
   {
      
   }
   
   public void CloseConnectionAndDelete()
   {
      CloseConnection();
      (this as ISelfDeletingObject<SenderTransferManager>).RemoveSelfFromParent();
   }
}

public class ReceiverTransferManager : TransferManagerBase, ISelfDeletingObject<ReceiverTransferManager>
{
   public ReceiverTransferManager(
      Socket socket,
      string rootDirectory,
      ISelfDeletingObjectParent<ReceiverTransferManager> parent)
      : base(socket, rootDirectory)
   {
      Parent = parent;
   }
   
   public ISelfDeletingObjectParent<ReceiverTransferManager> Parent { get; }
   
   protected override async Task CommunicateTransferParametersAsync(CancellationToken cancellationToken = default)
   {
      
   }
   
   protected override async Task HandleFileTransferAsync(CancellationToken cancellationToken = default)
   {
      
   }
   
   public void CloseConnectionAndDelete()
   {
      CloseConnection();
      (this as ISelfDeletingObject<ReceiverTransferManager>).RemoveSelfFromParent();
   }
}

public abstract class TransferManagerBase
{
   protected struct Header
   {
      public enum MessageType
      {
         FileName,
         FileHash,
         FileBlock,
         LastBlockReadResponse
      }

      public const int Size = 9;
      
      public MessageType Type { get; init; }
      public ulong DataSize { get; init; }
      
      public byte[] ToBytes()
      {
         var bytes = new byte[Size];
         bytes[0] = (byte)Type;
         BitConverter.TryWriteBytes(bytes.AsSpan(1, 8), DataSize);
         return bytes;
      }
      
      public static Header FromBytes(byte[] bytes)
      {
         return new Header
         {
            Type = (MessageType)bytes[0],
            DataSize = BitConverter.ToUInt64(bytes.AsSpan(1, 8))
         };
      }
   }
   
   protected struct FullMessage
   {
      public Header Header { get; init; }
      public byte[] Data { get; init; }
   }
      
   protected readonly CancellationTokenSource TransferCancellationTokenSource = new();
   protected readonly Socket Socket;
   
   // This is just the directory the file will be saved to, not the actual file path
   protected readonly string FileLocation;
   
   public TransferManagerBase(Socket socket, string fileLocation)
   {
      Socket = socket;
      FileLocation = fileLocation;

      Task.Run(async () => await RunAsync(TransferCancellationTokenSource.Token))
         .ContinueWith(_ => TransferCancellationTokenSource.Dispose());
   }

   public void CloseConnection()
   {
      TransferCancellationTokenSource.Cancel();
      Socket.Dispose();
   }

   protected async Task SendHeaderAsync(Header header, CancellationToken cancellationToken = default)
   {
      await Socket.SendAsync(header.ToBytes(), SocketFlags.None, cancellationToken);
   }
   
   protected async Task<Header> ReceiveHeaderAsync(CancellationToken cancellationToken = default)
   {
      var buffer = new byte[Header.Size];
      await Socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
      return cancellationToken.IsCancellationRequested ? default : Header.FromBytes(buffer);
   }
   
   protected async Task SendDataAsync(byte[] data, CancellationToken cancellationToken = default)
   {
      await Socket.SendAsync(data, SocketFlags.None, cancellationToken);
   }
   
   protected async Task<byte[]> ReceiveDataAsync(ulong dataSize, CancellationToken cancellationToken = default)
   {
      var buffer = new byte[dataSize];
      await Socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
      return cancellationToken.IsCancellationRequested ? Array.Empty<byte>() : buffer;
   }
   
   protected async Task<FullMessage> ReceiveFullMessageAsync(CancellationToken cancellationToken = default)
   {
      Header header = await ReceiveHeaderAsync(cancellationToken);
      var data = await ReceiveDataAsync(header.DataSize, cancellationToken);
      return cancellationToken.IsCancellationRequested ? default : new FullMessage
      {
         Header = header,
         Data = data
      };
   }
   
   protected async Task SendFullMessageAsync(FullMessage message, CancellationToken cancellationToken = default)
   {
      await SendHeaderAsync(message.Header, cancellationToken);
      await SendDataAsync(message.Data, cancellationToken);
   }
   
   protected abstract Task CommunicateTransferParametersAsync(CancellationToken cancellationToken = default);
   protected abstract Task HandleFileTransferAsync(CancellationToken cancellationToken = default);

   private async Task RunAsync(CancellationToken cancellationToken)
   {
      await CommunicateTransferParametersAsync(cancellationToken);
      await HandleFileTransferAsync(cancellationToken);
   }
}