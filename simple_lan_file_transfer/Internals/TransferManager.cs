namespace simple_lan_file_transfer.Internals;

public class SenderTransferManager
{
   private readonly Socket _socket;
   public SenderTransferManager(Socket socket)
   {
      _socket = socket;
   }

   public void Stop()
   {
      _socket.Dispose();
   }
}

public class ReceiverTransferManager
{
   private readonly Socket _socket;
   public ReceiverTransferManager(Socket socket)
   {
      _socket = socket;
   } 
   
   public void Stop()
   {
      _socket.Dispose();
   }
}

public abstract class TransferManagerBase
{
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