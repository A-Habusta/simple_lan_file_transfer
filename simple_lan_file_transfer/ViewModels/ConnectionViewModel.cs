
namespace simple_lan_file_transfer.ViewModels;

public class ConnectionViewModel : ViewModelBase
{
    public string TabName { get; set; }
    
    public ObservableCollection<IncomingTransferViewModel> IncomingTransfers { get; } = new();
    public ObservableCollection<OutgoingTransferViewModel> OutgoingTransfers { get; } = new();
    
    public ConnectionViewModel(string tabName)
    {
        TabName = tabName;
    }
    
    public void AddIncomingTransfer(IncomingTransferViewModel transfer)
    {
        IncomingTransfers.Add(transfer);
    }
    
    public void AddOutgoingTransfer(OutgoingTransferViewModel transfer)
    {
        OutgoingTransfers.Add(transfer);
    }
}