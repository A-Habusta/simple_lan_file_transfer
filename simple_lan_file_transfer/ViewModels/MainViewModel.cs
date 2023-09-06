using Avalonia.Platform.Storage;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using simple_lan_file_transfer.Models;
using simple_lan_file_transfer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace simple_lan_file_transfer.ViewModels;

public class MainViewModel : ViewModelBase
{
    public ObservableConcurrentDictionary<string, ConnectionTabViewModel> TabConnections { get; } = new();

    public ObservableCollection<string> AvailableIpAddresses { get; } = new();

    private readonly LocalNetworkAvailabilityBroadcastHandler _broadcastHandler = new();
    private readonly MasterConnectionManagerListenerWrapper _connectionManagerWrapper;

    // Do not access directly - use GetStorageProviderService() instead
    private StorageProviderWrapper? _storageProviderWrapper;

    private string _password = string.Empty;

    public MainViewModel()
    {
        _broadcastHandler.AvailableIpAddresses.CollectionChanged += OnAvailableIpAddressesChanged;

        var connectionManager = new MasterConnectionManager(Utility.DefaultPort);
        _connectionManagerWrapper = new MasterConnectionManagerListenerWrapper(connectionManager);

        _connectionManagerWrapper.NewIncomingConnection += OnNewIncomingConnection;
        _connectionManagerWrapper.RunLoop();
    }

    public void ChangeBroadcastTransmitState(bool newState)
    {
        if (newState)
        {
            _broadcastHandler.StartBroadcast();
        }
        else
        {
            _broadcastHandler.StopBroadcast();
        }
    }

    public void ChangeBroadcastListenState(bool newState)
    {
        if (newState)
        {
            _broadcastHandler.StartListening();
        }
        else
        {
            _broadcastHandler.StopListening();
        }
    }

    public void SaveNewPassword(string password)
    {
        _password = password;
    }

    public async Task OpenNewReceiveFolder()
    {
        await GetStorageProviderService().PickNewBookmarkedFolderAsync("Pick new receive folder");
    }

    public async Task StartFileSendAsync(string ipAddressString, string password)
    {
        if (!IPAddress.TryParse(ipAddressString, out IPAddress? ipAddress))
        {
            await ShowPopup($"Invalid IP address {ipAddressString}");
            return;
        }

        var files = await GetStorageProviderService().PickFilesAsync(pickerTitle: "Pick files to send");

        IStorageFile? savedFile = null;
        try
        {
            foreach (IStorageFile file in files)
            {
                savedFile = file;
                await CreateNewOutgoingTransferAsync(file, ipAddress, Utility.DefaultPort, password);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException
                                      or InvalidPasswordException or RemoteTransferCancelledException)
        {
            savedFile?.Dispose();
            await ShowPopup(ex.Message);
        }
    }

    private StorageProviderWrapper GetStorageProviderService()
    {
        if (_storageProviderWrapper is not null) return _storageProviderWrapper;

        var storageProviderService = App.Services?.GetRequiredService<IExposeStorageProviderService>();
        if (storageProviderService is null)
            throw new InvalidOperationException("Storage provider service is null.");

        _storageProviderWrapper = new StorageProviderWrapper(storageProviderService.StorageProvider);
        return _storageProviderWrapper;
    }

    private async Task CreateNewIncomingTransferAsync(Socket socket, CancellationToken cancellationToken = default)
    {
        IStorageFolder receiveRootFolder = await GetStorageProviderService().GetBookmarkedFolderAsync();
        using var receiveRootFolderWrapper = new StorageFolderWrapper(receiveRootFolder);

        TransferViewModel viewModel;
        try
        {
            viewModel = await TransferViewModel.TransferViewModelAsyncFactory.CreateIncomingTransferViewModelAsync(
                socket, receiveRootFolderWrapper, Utility.DefaultMetadataDirectory, _password, cancellationToken);
        }
        catch (LocalTransferCancelledException)
        {
            socket.Dispose();
            return;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException
                                      or InvalidPasswordException or RemoteTransferCancelledException)
        {
            socket.Dispose();
            await ShowPopup(ex.Message);
            return;
        }

        IPAddress ipAddress = ((IPEndPoint)socket.RemoteEndPoint!).Address;

        SaveTransferViewModelInCorrectTab(viewModel, ipAddress.ToString());
    }

    private async Task CreateNewOutgoingTransferAsync(IStorageFile file, IPAddress ipAddress, int port, string password,
        CancellationToken cancellationToken = default)
    {
        Socket socket = await MasterConnectionManager.StartNewOutgoingTransferAsync(ipAddress, port, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        TransferViewModel viewModel;
        try
        {
            viewModel = await TransferViewModel.TransferViewModelAsyncFactory.CreateOutgoingTransferViewModelAsync(
                socket, file, password, cancellationToken);
        }
        catch (Exception)
        {
            socket.Dispose();
            throw;
        }

        SaveTransferViewModelInCorrectTab(viewModel, ipAddress.ToString());
    }

    private void SaveTransferViewModelInCorrectTab(TransferViewModel transferViewModel, string destinationTabName) =>
        GetTabConnectionViewModel(destinationTabName).SaveTransferToCorrectCollection(transferViewModel);

    private ConnectionTabViewModel GetTabConnectionViewModel(string tabName)
    {
        var exists = TabConnections.TryGetValue(tabName, out ConnectionTabViewModel? tabConnectionViewModel);
        return exists ? tabConnectionViewModel! : CreateNewTabConnectionViewModel(tabName);
    }

    private ConnectionTabViewModel CreateNewTabConnectionViewModel(string tabName)
    {
        var result = new ConnectionTabViewModel(tabName);
        TabConnections.Add(tabName, result);

        return result;
    }

    private void OnAvailableIpAddressesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                foreach (IPAddress ipAddress in e.NewItems!.OfType<IPAddress>())
                {
                    var ipAddressString = ipAddress.ToString();
                    if (!AvailableIpAddresses.Contains(ipAddressString))
                    {
                        AvailableIpAddresses.Add(ipAddressString);
                    }
                }

                break;
            }
            case NotifyCollectionChangedAction.Remove:
            {
                foreach (IPAddress ipAddress in e.OldItems!.OfType<IPAddress>())
                {
                    AvailableIpAddresses.Remove(ipAddress.ToString());
                }

                break;
            }
            case NotifyCollectionChangedAction.Reset:
            {
                AvailableIpAddresses.Clear();
                break;
            }
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Replace:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(e));
        }
    }

    private async void OnNewIncomingConnection(object? sender, Socket socket)
    {
        await CreateNewIncomingTransferAsync(socket);
    }
}