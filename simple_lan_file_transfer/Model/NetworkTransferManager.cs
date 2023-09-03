namespace simple_lan_file_transfer.Models;

using System.Text;

public enum NetworkMessageType
{
   Metadata,
   Data,
   EndOfTransfer
}

public readonly struct NetworkMessage<T> 
{
   public T Data { get; init; }
   public NetworkMessageType Type { get; init; }
}

public sealed class NetworkTransferManager : IDisposable 
{
   private readonly struct Header
   {
      public const int Size = sizeof(byte) + sizeof(long);
      
      private readonly byte _type;
      public NetworkMessageType Type
      {
         get => (NetworkMessageType)_type;
         init => _type = (byte)value;
      }
      
      public long DataSize { get; init; }
      
      public byte[] ToBytes()
      {
         var bytes = new byte[Size];
         bytes[0] = _type;
         BitConverter.TryWriteBytes(bytes.AsSpan(1, 8), DataSize);
         return bytes;
      }
      
      public static Header FromBytes(byte[] bytes)
      {
         return new Header
         {
            Type = (NetworkMessageType)bytes[0],
            DataSize = BitConverter.ToInt64(bytes.AsSpan(1, 8))
         };
      }
   }
   
   private readonly struct FullMessage
   {
      public Header Header { get; init; }
      public byte[] Data { get; init; }
   }
   
   private bool _disposed;
   
   public void Dispose()
   {
      if (_disposed) return;
      
     
      _socket.Dispose();
      
      _disposed = true;
   }
   
   private readonly Socket _socket;
   
   public NetworkTransferManager(Socket socket)
   {
      _socket = socket;
   }

   public async Task SendAsync<T>(
      NetworkMessage<T> networkMessage,
      Func<T, byte[]> dataToBytes,
      CancellationToken cancellationToken = default)
   {
      cancellationToken.ThrowIfCancellationRequested();
      
      var buffer = dataToBytes(networkMessage.Data);
      var header = new Header
      {
         Type = networkMessage.Type,
         DataSize = buffer.Length
      };

      var fullMessage = new FullMessage
      {
         Header = header,
         Data = buffer
      };

      await SendFullMessageAsync(fullMessage, cancellationToken);
   }
   
   public async Task<NetworkMessage<T>> ReceiveAsync<T>(
      Func<byte[], T> bytesToData,
      CancellationToken cancellationToken = default)
   {
      cancellationToken.ThrowIfCancellationRequested();
      
      FullMessage fullMessage = await ReceiveFullMessageAsync(cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();

      return new NetworkMessage<T>
      {
         Data = bytesToData(fullMessage.Data),
         Type = fullMessage.Header.Type
      };
   }

   public async Task SendAsync(NetworkMessage<byte[]> message, CancellationToken cancellationToken = default)
   {
      await SendAsync(message, data => data, cancellationToken);
   }
   
   public async Task<NetworkMessage<byte[]>> ReceiveAsync(CancellationToken cancellationToken = default)
   {
      return await ReceiveAsync(data => data, cancellationToken);
   }

   private async Task SendHeaderAsync(Header header, CancellationToken cancellationToken = default)
   {
      var sent = await _socket.SendAsync(header.ToBytes(), SocketFlags.None, cancellationToken);
      
      cancellationToken.ThrowIfCancellationRequested();
      if (sent != Header.Size) throw new IOException("Failed to send all bytes");
   }
   
   private async Task<Header> ReceiveHeaderAsync(CancellationToken cancellationToken = default)
   {
      var buffer = new byte[Header.Size];
      var received = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
      
      cancellationToken.ThrowIfCancellationRequested();
      if (received != Header.Size) throw new IOException("Failed to receive all expected bytes");
      
      return Header.FromBytes(buffer);
   }
   
   private async Task SendDataAsync(byte[] data, CancellationToken cancellationToken = default)
   {
      var sent = await _socket.SendAsync(data, SocketFlags.None, cancellationToken);
      
      cancellationToken.ThrowIfCancellationRequested();
      if (sent != data.Length) throw new IOException("Failed to send all bytes");
   }
   
   private async Task<byte[]> ReceiveDataAsync(long dataSize, CancellationToken cancellationToken = default)
   {
      var buffer = new byte[dataSize];
      var received = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
      
      cancellationToken.ThrowIfCancellationRequested();
      if (received != dataSize) throw new IOException("Failed to receive all expected bytes");
      
      return buffer;
   }
   
   private async Task SendFullMessageAsync(FullMessage message, CancellationToken cancellationToken = default)
   {
      await SendHeaderAsync(message.Header, cancellationToken);
      await SendDataAsync(message.Data, cancellationToken);
   }
   
   private async Task<FullMessage> ReceiveFullMessageAsync(CancellationToken cancellationToken = default)
   {
      Header header = await ReceiveHeaderAsync(cancellationToken);
      var data = await ReceiveDataAsync(header.DataSize, cancellationToken);
      
      cancellationToken.ThrowIfCancellationRequested();

      return new FullMessage
      {
         Data = data,
         Header = header
      };
   }

}