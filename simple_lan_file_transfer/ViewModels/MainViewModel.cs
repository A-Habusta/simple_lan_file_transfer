using MsBox.Avalonia;
using Avalonia.Platform.Storage;
using simple_lan_file_transfer.Models;
using simple_lan_file_transfer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace simple_lan_file_transfer.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MasterConnectionManager ConnectionManager { get; } = new(Utility.DefaultPort);
    private StorageProviderWrapper? _fileProviderServiceWrapper = null;

    public void GetAndStoreStorageProviderService()
    {
        if (_fileProviderServiceWrapper is not null) return;

        var storageProviderService = App.Services?.GetRequiredService<IExposeStorageProviderService>();
        if (storageProviderService is null)
            throw new InvalidOperationException("Storage provider service is null.");

        _fileProviderServiceWrapper = new StorageProviderWrapper(storageProviderService.StorageProvider);
    }

    public ObservableCollection<ConnectionTabViewModel> TabConnections { get; } = new();

    public static async Task ShowOperationCancelledPopup() => await ShowPopup("Operation cancelled.");
    private static async Task ShowPopup(string message)
    {
        var popup = MessageBoxManager.GetMessageBoxStandard("Notice", message);
        await popup.ShowAsync();
    }

    public async Task CreateNewIncomingTransferAsync(Socket socket, CancellationToken cancellationToken = default)
    {
        IStorageFolder receiveRootFolder = await _fileProviderServiceWrapper.GetBookmarkedFolderAsync();
        using var receiveRootFolderWrapper = new StorageFolderWrapper(receiveRootFolder);

        TransferViewModel viewModel;
        try
        {
            viewModel = await TransferViewModel.TransferViewModelAsyncFactory.CreateIncomingTransferViewModelAsync(
                socket, receiveRootFolderWrapper, Utility.DefaultMetadataDirectory, cancellationToken);
        }
        catch (LocalTransferCancelledException)
        {
            socket.Dispose();
            return;
        }
        catch (Exception ex) when (ex is OperationCanceledException or RemoteTransferCancelledException or IOException
                                      or SocketException)
        {
            await ShowPopup(ex.Message);

            socket.Dispose();
            return;
        }

        IPAddress ipAddress = ((IPEndPoint)socket.RemoteEndPoint!).Address;

        CreateTransferViewModelInCorrectTab(viewModel, ipAddress.ToString());
    }

    public async Task CreateNewOutgoingTransferAsync(IStorageFile file, IPAddress ipAddress, int port,
        CancellationToken cancellationToken = default)
    {
        Socket socket;
        try
        {
            socket = await MasterConnectionManager.StartNewOutgoingTransferAsync(ipAddress, port, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException)
        {
            await ShowPopup(ex.Message);
            return;
        }

        TransferViewModel viewModel;
        try
        {
            viewModel = await TransferViewModel.TransferViewModelAsyncFactory.CreateOutgoingTransferViewModelAsync(
                socket, file, cancellationToken);
        }
        catch (LocalTransferCancelledException)
        {
            socket.Dispose();
            return;
        }
        catch (Exception ex) when (ex is OperationCanceledException or RemoteTransferCancelledException or IOException
                                      or SocketException)
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