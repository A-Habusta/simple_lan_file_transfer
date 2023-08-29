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