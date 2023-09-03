namespace simple_lan_file_transfer.Models;

using System.Text;

public sealed class SenderTransferManager : TransferManagerBase
{
   private string _fileName;
   
   public new ReaderFileAccessManager FileAccess
   {
      get => (ReaderFileAccessManager)base.FileAccess!;
      private init => base.FileAccess = value;
   }

   public SenderTransferManager(Socket socket, FileStream fileStream) : base(socket)
   {
      FileAccess = new ReaderFileAccessManager(fileStream);
      _fileName = Path.GetFileName(fileStream.Name);
   }

   public override async Task CommunicateTransferParametersAsync(CancellationToken cancellationToken = default)
   {
      await SendAsync(new Message<string>{ Data = _fileName, Type = MessageType.FileName }, cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();

      var fileHash = await FileAccess.GetFileHashAsync(cancellationToken);
      await SendAsync(new Message<byte[]> { Data = fileHash, Type = MessageType.FileHash }, cancellationToken);

      var lastBlockReadMessage = await ReceiveInt64Async(cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      
      FileAccess.SeekToBlock(lastBlockReadMessage.Data);
   }
   
   public override async Task RunFileTransferAsync(CancellationToken cancellationToken = default)
   {
      for (;;)
      {
         var block = FileAccess.ReadNextBlock();
         await SendAsync(new Message<byte[]>{ Data = block, Type = MessageType.FileBlock}, cancellationToken);
         cancellationToken.ThrowIfCancellationRequested();
         
         FileAccess.IncrementBlockCounter();

         if (block.LongLength >= Utility.BlockSize) continue;
         
         await SendEndOfTransferMessage(cancellationToken);
         break;
      }
   }

   protected override void Dispose(bool disposing)
   {
      if (Disposed) return;

      if (disposing)
      {
         FileAccess.Dispose();
      }
      
      base.Dispose(disposing);
   }
}

public class ReceiverTransferManager : TransferManagerBase
{
   public new WriterFileAccessManager? FileAccess
   {
      get => (WriterFileAccessManager?)base.FileAccess;
      private set => base.FileAccess = value;
   }
   private readonly string _rootDirectory;
   
   public ReceiverTransferManager(Socket socket, string rootDirectory) : base(socket)
   {
      _rootDirectory = rootDirectory;
   }

   public override async Task CommunicateTransferParametersAsync(CancellationToken cancellationToken = default)
   {
      var originalFileNameMessage = await ReceiveStringAsync(cancellationToken);
      var fileBlockCountMessage = await ReceiveInt64Async(cancellationToken);
      
      cancellationToken.ThrowIfCancellationRequested();
      
      // TODO check if user wants to change filename
      FileAccess = new WriterFileAccessManager(_rootDirectory, originalFileNameMessage.Data)
      {
         FileBlocksCount = fileBlockCountMessage.Data
      };
      
      var fileHashMessage = await ReceiveBytesAsync(cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      
      FileAccess.OpenMetadataFile(fileHashMessage.Data);

      var lastBlockRead = FileAccess.ReadFileLastWrittenBlock();
      await SendAsync(new Message<long>{ Data = lastBlockRead, Type = MessageType.LastBlockReadResponse }, cancellationToken);
   }
   
   public override async Task RunFileTransferAsync(CancellationToken cancellationToken = default)
   {
      for (;;)
      {
         var message = await ReceiveBytesAsync(cancellationToken);
         cancellationToken.ThrowIfCancellationRequested();

         if (message.Type == MessageType.EndOfTransfer) break;
         
         FileAccess?.WriteNextBlock(message.Data);
         FileAccess?.IncrementBlockCounter();
      }
   }
   
   protected override void Dispose(bool disposing)
   {
      if (Disposed) return;

      if (disposing)
      {
         FileAccess?.Dispose();
      }
      
      base.Dispose(disposing);
   }
}

public abstract class TransferManagerBase : IDisposable 
{
   protected enum MessageType
   {
      FileName,
      FileHash,
      FileBlock,
      LastBlockReadResponse,
      EndOfTransfer
   }
   
   protected readonly struct Header
   {
      public const int Size = 9;
      
      public MessageType Type { get; init; }
      public long DataSize { get; init; }
      
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
            DataSize = BitConverter.ToInt64(bytes.AsSpan(1, 8))
         };
      }
   }
   
   protected readonly struct FullMessage
   {
      public Header Header { get; init; }
      public byte[] Data { get; init; }
   }

   protected readonly struct Message<T>
   {
      public T Data { get; init; }
      public MessageType Type { get; init; }
   }
   
   protected bool Disposed;
   
   public FileAccessManager? FileAccess { get; protected set; }
      
   private readonly CancellationTokenSource _transferCancellationTokenSource = new();
   private readonly Socket _socket;
   
   protected TransferManagerBase(Socket socket)
   {
      _socket = socket;
   }
   
   public void Dispose()
   {
      if (Disposed) return;
      
      Dispose(true);
      GC.SuppressFinalize(this);
   }
   
   protected virtual void Dispose(bool disposing)
   {
      if (Disposed) return;
      
      if (disposing)
      {
         _transferCancellationTokenSource.Cancel();
         _transferCancellationTokenSource.Dispose();
      }
      
      Disposed = true;
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

   protected async Task SendEndOfTransferMessage(CancellationToken cancellationToken = default)
   {
      var message = new Message<byte[]>
      {
         Data = Array.Empty<byte>(),
         Type = MessageType.EndOfTransfer
      };

      await SendAsync(message, cancellationToken);
   }
   
   protected async Task SendAsync(Message<byte[]> message, CancellationToken cancellationToken = default)
   {
      var header = new Header
      {
         Type = message.Type,
         DataSize = message.Data.LongLength
      };
      var fullMessage = new FullMessage
      {
         Header = header,
         Data = message.Data
      };
      
      await SendFullMessageAsync(fullMessage, cancellationToken);
   }
   
   protected async Task SendAsync(Message<string> message, CancellationToken cancellationToken = default)
   {
      await SendAsync(message, Encoding.UTF8.GetBytes, cancellationToken);
   }
   
   protected async Task SendAsync(Message<long> message, CancellationToken cancellationToken = default)
   {
      await SendAsync(message, BitConverter.GetBytes, cancellationToken);
   }
   
   protected async Task<Message<byte[]>> ReceiveBytesAsync(CancellationToken cancellationToken = default)
   {
      return await ReceiveAsync(bytes => bytes, cancellationToken);
   }
   
   protected async Task<Message<string>> ReceiveStringAsync(CancellationToken cancellationToken = default)
   {
      return await ReceiveAsync(bytes => Encoding.UTF8.GetString(bytes), cancellationToken);
   }
   
   protected async Task<Message<long>> ReceiveInt64Async(CancellationToken cancellationToken = default)
   {
      return await ReceiveAsync(bytes => BitConverter.ToInt64(bytes), cancellationToken);
   }
   
   
   public abstract Task CommunicateTransferParametersAsync(CancellationToken cancellationToken = default);
   public abstract Task RunFileTransferAsync(CancellationToken cancellationToken = default);
   
   private async Task SendAsync<T>(
      Message<T> message,
      Func<T, byte[]> dataToBytes,
      CancellationToken cancellationToken = default)
   {
      var outputMessage = new Message<byte[]>
      {
         Data = dataToBytes(message.Data),
         Type = message.Type
      };
      
      await SendAsync(outputMessage, cancellationToken);
   }
   
   private async Task<Message<T>> ReceiveAsync<T>(
      Func<byte[], T> bytesToData,
      CancellationToken cancellationToken = default)
   {
      FullMessage fullMessage = await ReceiveFullMessageAsync(cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();

      if (fullMessage.Data.LongLength != fullMessage.Header.DataSize)
      {
         throw new IOException("Received unexpected data size");
      }
      cancellationToken.ThrowIfCancellationRequested();

      return new Message<T>
      {
         Data = bytesToData(fullMessage.Data),
         Type = fullMessage.Header.Type
      };
   }

   private async Task SendHeaderAsync(Header header, CancellationToken cancellationToken = default)
   {
      await _socket.SendAsync(header.ToBytes(), SocketFlags.None, cancellationToken);
   }
   
   private async Task<Header> ReceiveHeaderAsync(CancellationToken cancellationToken = default)
   {
      var buffer = new byte[Header.Size];
      await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
      return cancellationToken.IsCancellationRequested ? default : Header.FromBytes(buffer);
   }
   
   private async Task SendDataAsync(byte[] data, CancellationToken cancellationToken = default)
   {
      await _socket.SendAsync(data, SocketFlags.None, cancellationToken);
   }
   
   private async Task<byte[]> ReceiveDataAsync(long dataSize, CancellationToken cancellationToken = default)
   {
      var buffer = new byte[dataSize];
      await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
      return cancellationToken.IsCancellationRequested ? Array.Empty<byte>() : buffer;
   }
}