namespace simple_lan_file_transfer.Internals;

using System.Text;

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
      await SendStringAsync(_fileName, MessageType.FileName, cancellationToken);
      if (cancellationToken.IsCancellationRequested) return;

      var fileHash = Array.Empty<byte>(); // TODO

      await SendBytesAsync(fileHash, MessageType.FileHash, cancellationToken);

      var lastBlockRead = await ReceiveUnsignedLongAsync(MessageType.LastBlockReadResponse, cancellationToken);
      //TODO finish configuration
   }
   
   protected override async Task HandleFileTransferAsync(CancellationToken cancellationToken = default)
   {
      // TODO program file manager
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
       var originalFileName = await ReceiveStringAsync(MessageType.FileName, cancellationToken);
       
       // TODO check if user wants to change filename
       
       var fileHash = await ReceiveBytesAsync(MessageType.FileHash, cancellationToken);
       
       // TODO save hash
       
       var lastBlockRead = Array.Empty<byte>(); // TODO get last block read
       
       await SendBytesAsync(lastBlockRead, MessageType.LastBlockReadResponse, cancellationToken);
       
       // TODO finish configuration
   }
   
   protected override async Task HandleFileTransferAsync(CancellationToken cancellationToken = default)
   {
      // TODO program file manager
   }
   
   public void CloseConnectionAndDelete()
   {
      CloseConnection();
      (this as ISelfDeletingObject<ReceiverTransferManager>).RemoveSelfFromParent();
   }
}

public abstract class TransferManagerBase
{
   protected enum MessageType
   {
      FileName,
      FileHash,
      FileBlock,
      LastBlockReadResponse
   }
   
   protected readonly struct Header
   {
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
   
   protected readonly struct FullMessage
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
   
   protected async Task SendFullMessageAsync(FullMessage message, CancellationToken cancellationToken = default)
   {
      await SendHeaderAsync(message.Header, cancellationToken);
      await SendDataAsync(message.Data, cancellationToken);
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
   
   protected async Task SendBytesAsync(byte[] bytes, MessageType messageType, CancellationToken cancellationToken = default)
   {
      var header = new Header
      {
         Type = messageType,
         DataSize = (ulong)bytes.Length
      };
      var fullMessage = new FullMessage
      {
         Header = header,
         Data = bytes
      };
      
      await SendFullMessageAsync(fullMessage, cancellationToken);
   }
   
   protected async Task<byte[]?> ReceiveBytesAsync(MessageType messageType, CancellationToken cancellationToken = default)
   {
      return await ReceiveAsync(messageType, bytes => bytes, cancellationToken);
   }
   
   protected async Task SendStringAsync(string str, MessageType messageType, CancellationToken cancellationToken = default)
   {
      await SendAsync(str, messageType, Encoding.UTF8.GetBytes, cancellationToken);
   }
   
   protected async Task<string?> ReceiveStringAsync(MessageType messageType, CancellationToken cancellationToken = default)
   {
      return await ReceiveAsync(messageType, bytes => Encoding.UTF8.GetString(bytes), cancellationToken);
   }
   
   protected async Task SendUnsignedLongAsync(ulong num, MessageType messageType, CancellationToken cancellationToken = default)
   {
      await SendAsync(num, messageType, BitConverter.GetBytes, cancellationToken);
   }
   
   protected async Task<ulong?> ReceiveUnsignedLongAsync(MessageType messageType, CancellationToken cancellationToken = default)
   {
      return await ReceiveAsync(messageType, bytes => BitConverter.ToUInt64(bytes), cancellationToken);
   }
   
   
   protected abstract Task CommunicateTransferParametersAsync(CancellationToken cancellationToken = default);
   protected abstract Task HandleFileTransferAsync(CancellationToken cancellationToken = default);
   
   private async Task SendAsync<T>(
      T data,
      MessageType messageType,
      Func<T, byte[]> dataToBytes,
      CancellationToken cancellationToken = default)
   {
      await SendBytesAsync(dataToBytes(data), messageType, cancellationToken);
   }
   
   private async Task<T?> ReceiveAsync<T>(
      MessageType expectedMessageType,
      Func<byte[], T> bytesToData,
      CancellationToken cancellationToken = default)
   {
      FullMessage fullMessage = await ReceiveFullMessageAsync(cancellationToken);
      if (cancellationToken.IsCancellationRequested) return default;
      
      if (fullMessage.Header.Type != expectedMessageType)
      {
         throw new IOException("Received unexpected message type");
      }
      
      if ((ulong)fullMessage.Data.Length != fullMessage.Header.DataSize)
      {
         throw new IOException("Received unexpected data size");
      }
      
      return cancellationToken.IsCancellationRequested ? default : bytesToData(fullMessage.Data);
   }

   private async Task SendHeaderAsync(Header header, CancellationToken cancellationToken = default)
   {
      await Socket.SendAsync(header.ToBytes(), SocketFlags.None, cancellationToken);
   }
   
   private async Task<Header> ReceiveHeaderAsync(CancellationToken cancellationToken = default)
   {
      var buffer = new byte[Header.Size];
      await Socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
      return cancellationToken.IsCancellationRequested ? default : Header.FromBytes(buffer);
   }
   
   private async Task SendDataAsync(byte[] data, CancellationToken cancellationToken = default)
   {
      await Socket.SendAsync(data, SocketFlags.None, cancellationToken);
   }
   
   private async Task<byte[]> ReceiveDataAsync(ulong dataSize, CancellationToken cancellationToken = default)
   {
      var buffer = new byte[dataSize];
      await Socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
      return cancellationToken.IsCancellationRequested ? Array.Empty<byte>() : buffer;
   }

   private async Task RunAsync(CancellationToken cancellationToken)
   {
      await CommunicateTransferParametersAsync(cancellationToken);
      await HandleFileTransferAsync(cancellationToken);
   }
}