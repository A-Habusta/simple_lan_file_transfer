using ReactiveUI;
using simple_lan_file_transfer.Models;

namespace simple_lan_file_transfer.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MasterConnectionManager ConnectionManager { get; } = new(Utility.DefaultPort);
    public ObservableCollection<TabConnectionViewModel> TabConnections { get; } = new();

    private string _specialMessage = string.Empty;
    public string SpecialMessage
    {
        get => _specialMessage;
        set => this.RaiseAndSetIfChanged(ref _specialMessage, value);
    }

    public void ShowOperationCanceledPopup() => ShowPopup("Operation canceled.");
    public void ShowPopup(string message)
    {
        throw new NotImplementedException();
    }

    public async Task CreateNewOutgoingTransferAsync(string filePath, IPAddress ipAddress, int port,
        CancellationToken cancellationToken = default)
    {
        Socket socket;
        try
        {
            socket = await MasterConnectionManager.StartNewOutgoingTransferAsync(ipAddress, port, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        // TODO: Handle specific exceptions
        catch (Exception ex)
        {
            ShowPopup(ex.Message);
            return;
        }

        TransferViewModel viewModel;
        try
        {
            viewModel = await TransferViewModel.TransferViewModelAsyncFactory.CreateOutgoingTransferViewModelAsync(
                socket, filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            ShowPopup(ex.Message);
            socket.Dispose();
            return;
        }

        CreateTransferViewModelInCorrectTab(viewModel, ipAddress.ToString());
    }

    private void CreateTransferViewModelInCorrectTab(TransferViewModel transferViewModel, string destinationTabName) =>
        GetTabConnectionViewModel(destinationTabName).SaveTransferToCorrectCollection(transferViewModel);

    private TabConnectionViewModel GetTabConnectionViewModel(string tabName)
    {
        TabConnectionViewModel result = TabConnections.FirstOrDefault(x => x.TabName == tabName) ??
                                        CreateNewTabConnectionViewModel(tabName);

        return result;
    }

    private TabConnectionViewModel CreateNewTabConnectionViewModel(string tabName)
    {
        var result = new TabConnectionViewModel(tabName);
        TabConnections.Add(result);

        return result;
    }
}