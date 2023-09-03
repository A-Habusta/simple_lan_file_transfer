using ReactiveUI;
using simple_lan_file_transfer.Models;

namespace simple_lan_file_transfer.ViewModels;

public abstract class TransferViewModel : ViewModelBase
{
    protected TransferViewModel(TransferManagerBase manager)
    {
        TransferManager = manager;
        manager.FileAccess!.PropertyChanged += OnFileAccessPropertyChanged;
    }
    
    public TransferManagerBase TransferManager { get; }

    private double? _progress;
    public double? Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }
    
    private void OnFileAccessPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileAccessManager.UsedBlockCounter))
        {
            OnProgressChanged();
        }
    }

    private void OnProgressChanged()
    {
        Progress = TransferManager.FileAccess?.GetProgress();
    }
}

public class IncomingTransferViewModel : TransferViewModel
{
    public new ReceiverTransferManager TransferManager => (ReceiverTransferManager)base.TransferManager;
    public IncomingTransferViewModel(ReceiverTransferManager manager) : base(manager) { }
}

public class OutgoingTransferViewModel : TransferViewModel
{
    public new SenderTransferManager TransferManager => (SenderTransferManager)base.TransferManager;
    public OutgoingTransferViewModel(SenderTransferManager manager) : base(manager) { }
}