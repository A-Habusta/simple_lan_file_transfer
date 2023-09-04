using MsBox.Avalonia;
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

    private async Task ShowOperationCanceledPopup() => await ShowPopup("Operation cancelled.");
    private async Task ShowPopup(string message)
    {
        var popup = MessageBoxManager.GetMessageBoxStandard("Notice", message);
        await popup.ShowAsync();
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
            await ShowPopup(ex.Message);
            return;
        }

        TransferViewModel viewModel;
        try
        {
            viewModel = await TransferViewModel.TransferViewModelAsyncFactory.CreateOutgoingTransferViewModelAsync(
                socket, filePath, cancellationToken);
        }
        catch (LocalTransferCancelledException)
        {
            socket.Dispose();
            return;
        }
        catch (Exception ex)
        {
            await ShowPopup(ex.Message);

            socket.Dispose();
            return;
        }

        CreateTransferViewModelInCorrectTab(viewModel, ipAddress.ToString());
    }

    private void CreateTransferViewModelInCorrectTab(TransferViewModel transferViewModel, string destinationTabName) =>
        GetTabConnectionViewModel(destinationTabName).SaveTransferToCorrectCollection(transferViewModel);

    private ConnectionTabViewModel GetTabConnectionViewModel(string tabName)
    {
        ConnectionTabViewModel result = TabConnections.FirstOrDefault(x => x.TabName == tabName) ??
                                        CreateNewTabConnectionViewModel(tabName);

        return result;
    }

    private ConnectionTabViewModel CreateNewTabConnectionViewModel(string tabName)
    {
        var result = new ConnectionTabViewModel(tabName);
        TabConnections.Add(result);

        return result;
    }
}