namespace simple_lan_file_transfer.Internals;

public class SenderTransferManager : TransferManagerBase
{
   public SenderTransferManager(Socket socket) : base(socket) {}
}

public class ReceiverTransferManager : TransferManagerBase
{
   public ReceiverTransferManager(Socket socket) : base(socket) {}
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
      
   protected readonly Socket Socket;
   public TransferManagerBase(Socket socket)
   {
      Socket = socket;
   }
   
   public void Stop()
   {
      Socket.Dispose();
   }
}