using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace simple_lan_file_transfer.Services;

public class ExposeStorageProviderService : IExposeStorageProviderService
{
    private Visual? _mainVisual;
    private IStorageProvider? _storageProvider;
    public IStorageProvider StorageProvider => _storageProvider ?? InitializeStorageProviderFromVisual();


    public ExposeStorageProviderService(TopLevel window)
    {
        _storageProvider = window.StorageProvider;
    }

    public ExposeStorageProviderService(Visual control)
    {
        _mainVisual = control;
    }

    public IStorageProvider InitializeStorageProviderFromVisual()
    {
        if (_mainVisual is null) throw new InvalidOperationException("Visual is null.");

        try
        {
            _storageProvider = TopLevel.GetTopLevel(_mainVisual)!.StorageProvider;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
        {
            throw new InvalidOperationException("Can't get storage provider from visual.");
        }

        return _storageProvider;
    }
}