namespace simple_lan_file_transfer.Internals;

public class SenderTransferManager
{
   private Socket _socket;
   public SenderTransferManager(Socket socket)
   {
      _socket = socket;
   } 
}

public class ReceiverTransferManager
{
   private Socket _socket;
   public ReceiverTransferManager(Socket socket)
   {
      _socket = socket;
   } 
}