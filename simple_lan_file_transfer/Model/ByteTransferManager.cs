namespace simple_lan_file_transfer.Models;

public enum MessageType
{
   Metadata,
   Data,
   EndOfTransfer
}

public readonly struct ReceiveResult<T>
{
   public T Data { get; init; }
   public MessageType Type { get; init; }
}

public interface IByteSenderAsync
{
   Task SendAsync<T>(
      MessageType type,
      T data,
      Func<T, ReadOnlyMemory<byte>?> dataToBytes,
      CancellationToken cancellationToken = default);

   Task SendAsync(MessageType type, ReadOnlyMemory<byte> data = default, CancellationToken cancellationToken = default) =>
      SendAsync(type, data , x => x, cancellationToken);
}

public interface IByteReceiverAsync
{
   Task<ReceiveResult<T>> ReceiveAsync<T>(
      Func<ReadOnlyMemory<byte>, T> bytesToData,
      CancellationToken cancellationToken = default);

   Task<ReceiveResult<ReadOnlyMemory<byte>>> ReceiveAsync(CancellationToken cancellationToken = default) =>
      ReceiveAsync(data => data, cancellationToken);
}

public interface IByteTransferManagerAsync : IByteSenderAsync, IByteReceiverAsync {}

public sealed class NetworkTransferManagerAsync : IDisposable, IByteTransferManagerAsync
{
   private readonly struct Header
   {
      public const int Size = sizeof(byte) + sizeof(int);

      private readonly byte _type;
      public MessageType Type
      {
         get => (MessageType)_type;
         init => _type = (byte)value;
      }

      public int DataSize { get; init; }

      public ReadOnlyMemory<byte> ToBytes()
      {
         var bytes = new byte[Size];
         bytes[0] = _type;
         BitConverter.TryWriteBytes(bytes.AsSpan(1, sizeof(int)), DataSize);
         return new ReadOnlyMemory<byte>(bytes);
      }

      public static Header FromBytes(ReadOnlyMemory<byte> bytes)
      {
         return new Header
         {
            Type = (MessageType) bytes.Span[0],
            DataSize = BitConverter.ToInt32(bytes.Slice(1, sizeof(int)).Span)
         };
      }
   }

   private readonly struct FullMessage
   {
      public Header Header { get; init; }
      public ReadOnlyMemory<byte> Data { get; init; }
   }

   private bool _disposed;

   private readonly Socket _socket;
   private readonly byte[] _blockBuffer = new byte[Utility.BlockSize];

   public NetworkTransferManagerAsync(Socket socket)
   {
      _socket = socket;
   }

   public async Task SendAsync<T>(
      MessageType type,
      T data,
      Func<T, ReadOnlyMemory<byte>?> dataToBytes,
      CancellationToken cancellationToken = default)
   {
      if (_disposed) throw new ObjectDisposedException(nameof(NetworkTransferManagerAsync));
      cancellationToken.ThrowIfCancellationRequested();

      var convertedData = dataToBytes(data) ?? ReadOnlyMemory<byte>.Empty;
      var header = new Header
      {
         Type = type,
         DataSize = convertedData.Length
      };

      var fullMessage = new FullMessage
      {
         Header = header,
         Data = convertedData
      };

      await SendFullMessageAsync(fullMessage, cancellationToken);
   }

   public async Task<ReceiveResult<T>> ReceiveAsync<T>(
      Func<ReadOnlyMemory<byte>, T> bytesToData,
      CancellationToken cancellationToken = default)
   {
      if (_disposed) throw new ObjectDisposedException(nameof(NetworkTransferManagerAsync));
      cancellationToken.ThrowIfCancellationRequested();

      FullMessage fullMessage = await ReceiveFullMessageAsync(cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();

      return new ReceiveResult<T>
      {
         Data = bytesToData(fullMessage.Data),
         Type = fullMessage.Header.Type
      };
   }

   public void Dispose()
   {
      if (_disposed) return;

      _socket.Dispose();

      _disposed = true;
   }

   private async Task SendFullMessageAsync(FullMessage message, CancellationToken cancellationToken = default)
   {
      await SendHeaderAsync(message.Header, cancellationToken);
      if (message.Header.DataSize == 0) return;

      await SendDataAsync(message.Data, cancellationToken);
   }

   private async Task<FullMessage> ReceiveFullMessageAsync(CancellationToken cancellationToken = default)
   {
      Header header = await ReceiveHeaderAsync(cancellationToken);

      var data = header.DataSize > 0 ? await ReceiveDataAsync(header.DataSize, cancellationToken)
                                                        : ReadOnlyMemory<byte>.Empty;

      cancellationToken.ThrowIfCancellationRequested();

      return new FullMessage
      {
         Data = data,
         Header = header
      };
   }

   private async Task SendHeaderAsync(Header header, CancellationToken cancellationToken = default)
      => await SendRawDataAsync(header.ToBytes(), cancellationToken);

   private async Task<Header> ReceiveHeaderAsync(CancellationToken cancellationToken = default)
   {
      var headerBytes = await ReceiveRawDataAsync(Header.Size, cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();

      return Header.FromBytes(headerBytes);
   }

   private async Task SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) =>
      await SendRawDataAsync(data, cancellationToken);

   private async Task<ReadOnlyMemory<byte>> ReceiveDataAsync(int dataSize, CancellationToken cancellationToken = default)
      => await ReceiveRawDataAsync(dataSize, cancellationToken);

   // This method works with the assumption that the entire data array is to be sent
   private async Task SendRawDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
   {
      var sent = 0;
      while (sent < data.Length)
      {
         var toSend = data.Length - sent;
         var currentSent = await _socket.SendAsync(data.Slice(sent, toSend), cancellationToken);

         if (currentSent == 0) throw new IOException("Remote connection closed");

         sent += currentSent;
      }
   }

   private async Task<ReadOnlyMemory<byte>> ReceiveRawDataAsync(int size, CancellationToken cancellationToken = default)
   {
      if (size > _blockBuffer.Length)
         
      {
         throw new ArgumentOutOfRangeException(
            nameof(size),
            $"Trying to receive {size} bytes into a {_blockBuffer} sized buffer");
      }

      var received = 0;
      while(received < size)
      {
         var toReceive = size - received;
         var currentReceived = await _socket.ReceiveAsync(_blockBuffer.AsMemory(received, toReceive), cancellationToken);

         if (currentReceived == 0) throw new IOException("Remote connection closed");

         received += currentReceived;
      }

      return new ReadOnlyMemory<byte>(_blockBuffer, 0, size);
   }

}