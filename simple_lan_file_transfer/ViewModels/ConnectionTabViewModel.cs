namespace simple_lan_file_transfer.ViewModels;

/// <summary>
/// ViewModel which holds information necessary to display a single connection tab.
/// </summary>
public class ConnectionTabViewModel : ViewModelBase
{
    public string TabName { get; }
    public ObservableCollection<TransferViewModel> OutgoingTransfers { get; } = new();
    public ObservableCollection<TransferViewModel> IncomingTransfers { get; } = new();

    public ConnectionTabViewModel(string tabName)
    {
        TabName = tabName;
    }

    /// <summary>
    /// Saves transfer to the correct collection based on the transfer direction.
    /// </summary>
    /// <param name="transferViewModel">Transfer to be saved</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when and invalid transfer direction is given</exception>
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

    /// <summary>
    /// Removes transfer from the correct collection based on the transfer direction.
    /// </summary>
    /// <param name="transferViewModel">Transfer to be removed</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when and invalid transfer direction is given</exception>
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