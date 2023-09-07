using Avalonia.Platform.Storage;

namespace simple_lan_file_transfer.Services;

public interface IExposeStorageProviderService
{
    IStorageProvider StorageProvider { get; }
}