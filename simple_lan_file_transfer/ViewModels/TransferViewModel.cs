using simple_lan_file_transfer.Internals;

namespace simple_lan_file_transfer.ViewModels;

public abstract class TransferViewModel<TManager, TFileAccess> : ViewModelBase
    where TManager : TransferManagerBase<TFileAccess>
    where TFileAccess : FileAccessManager
{
    protected TransferViewModel(TManager manager)
    {
        Manager = manager;
    }
    
    public TManager Manager { get; }
    public double? Progress => Manager.FileAccess?.GetProgress();
}

public class IncomingTransferViewModel : TransferViewModel<ReceiverTransferManager, WriterFileAccessManager>
{
    public IncomingTransferViewModel(ReceiverTransferManager manager) : base(manager) { }
}

public class OutgoingTransferViewModel : TransferViewModel<SenderTransferManager, ReaderFileAccessManager>
{
    public OutgoingTransferViewModel(SenderTransferManager manager) : base(manager) { }
}