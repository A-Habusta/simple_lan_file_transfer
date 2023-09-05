namespace simple_lan_file_transfer.ViewModels;

public class ConnectionTabViewModel : ViewModelBase
{
    public string TabName { get; }
    public ObservableCollection<TransferViewModel> OutgoingTransfers { get; } = new();
    public ObservableCollection<TransferViewModel> IncomingTransfers { get; } = new();

    public ConnectionTabViewModel(string tabName)
    {
        TabName = tabName;
    }

    public void SaveTransferToCorrectCollection(TransferViewModel transferViewModel)
    {
        switch (transferViewModel.Direction)
        {
            case TransferViewModel.TransferDirection.Incoming:
                IncomingTransfers.Add(transferViewModel);
                break;
            case TransferViewModel.TransferDirection.Outgoing:
                OutgoingTransfers.Add(transferViewModel);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(transferViewModel));
        }
        transferViewModel.RegisterSelfRemover(RemoveTransferFromCorrectCollection);
    }

    public void RemoveTransferFromCorrectCollection(TransferViewModel transferViewModel)
    {
        switch (transferViewModel.Direction)
        {
            case TransferViewModel.TransferDirection.Incoming:
                IncomingTransfers.Remove(transferViewModel);
                break;
            case TransferViewModel.TransferDirection.Outgoing:
                OutgoingTransfers.Remove(transferViewModel);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(transferViewModel));
        }
    }
}