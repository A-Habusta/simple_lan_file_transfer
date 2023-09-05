using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace simple_lan_file_transfer.Services;

public class ExposeStorageProviderService : IExposeStorageProviderService
{
    public IStorageProvider StorageProvider { get; }

    public ExposeStorageProviderService(TopLevel window)
    {
        StorageProvider = window.StorageProvider;
    }

    public ExposeStorageProviderService(Visual control)
    {
        StorageProvider = TopLevel.GetTopLevel(control)!.StorageProvider;
    }
}