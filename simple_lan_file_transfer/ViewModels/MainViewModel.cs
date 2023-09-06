﻿using MsBox.Avalonia;
using Avalonia.Platform.Storage;
using simple_lan_file_transfer.Models;
using simple_lan_file_transfer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace simple_lan_file_transfer.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MasterConnectionManager ConnectionManager { get; } = new(Utility.DefaultPort);

    // Do not access directly - use GetStorageProviderService() instead
    private StorageProviderWrapper? _storageProviderWrapper;

    private string _password = string.Empty;

    private StorageProviderWrapper GetStorageProviderService()
    {
        if (_storageProviderWrapper is not null) return _storageProviderWrapper;

        var storageProviderService = App.Services?.GetRequiredService<IExposeStorageProviderService>();
        if (storageProviderService is null)
            throw new InvalidOperationException("Storage provider service is null.");

        _storageProviderWrapper = new StorageProviderWrapper(storageProviderService.StorageProvider);
        return _storageProviderWrapper;
    }

    public ObservableCollection<ConnectionTabViewModel> TabConnections { get; } = new();

    public async Task StartFileSendAsync(string ipAddress, string password)
    {
        if (!IPAddress.TryParse(ipAddress, out IPAddress? ipAddressObj))
        {
            await ShowPopup($"Invalid IP address {ipAddress}.");
            return;
        }

        var files = await GetStorageProviderService().PickFilesAsync(pickerTitle: "Pick files to send");

        IStorageFile? savedFile = null;
        try
        {
            foreach (IStorageFile file in files)
            {
                savedFile = file;
                await CreateNewOutgoingTransferAsync(file, ipAddressObj, Utility.DefaultPort, password);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException
                                      or InvalidPasswordException or RemoteTransferCancelledException)
        {
            savedFile?.Dispose();
            await ShowPopup(ex.Message);
        }
    }

    public void SaveNewPassword(string password)
    {
        _password = password;
    }

    private static async Task ShowPopup(string message)
    {
        var popup = MessageBoxManager.GetMessageBoxStandard("Notice", message);
        await popup.ShowAsync();
    }

    private async Task CreateNewIncomingTransferAsync(Socket socket, CancellationToken cancellationToken = default)
    {
        IStorageFolder receiveRootFolder = await GetStorageProviderService().GetBookmarkedFolderAsync();
        using var receiveRootFolderWrapper = new StorageFolderWrapper(receiveRootFolder);

        TransferViewModel viewModel;
        try
        {
            viewModel = await TransferViewModel.TransferViewModelAsyncFactory.CreateIncomingTransferViewModelAsync(
                socket, receiveRootFolderWrapper, Utility.DefaultMetadataDirectory, _password,
                cancellationToken);
        }
        catch (Exception)
        {
            socket.Dispose();
            throw;
        }

        IPAddress ipAddress = ((IPEndPoint)socket.RemoteEndPoint!).Address;

        SaveTransferViewModelInCorrectTab(viewModel, ipAddress.ToString());
    }

    private async Task CreateNewOutgoingTransferAsync(IStorageFile file, IPAddress ipAddress, int port, string password,
        CancellationToken cancellationToken = default)
    {
        Socket socket;
        socket = await MasterConnectionManager.StartNewOutgoingTransferAsync(ipAddress, port, cancellationToken);
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