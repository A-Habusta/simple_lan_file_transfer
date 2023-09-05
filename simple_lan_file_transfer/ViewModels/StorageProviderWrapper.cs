using Avalonia.Platform.Storage;

namespace simple_lan_file_transfer.ViewModels;

public class StorageProviderWrapper
{
    private readonly IStorageProvider _storageProvider;
    private string _bookmark = string.Empty;


    public StorageProviderWrapper(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }

    public async Task<IEnumerable<IStorageFile>> PickFilesAsync(string pickerTitle)
    {
        var pickerOptions = new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = pickerTitle
        };

        return await _storageProvider.OpenFilePickerAsync(pickerOptions);
    }

    public async Task PickNewBookmarkedFolderAsync(string pickerTitle)
    {
        var folderPickerOptions = new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = pickerTitle
        };
        var folders = await _storageProvider.OpenFolderPickerAsync(folderPickerOptions);

        // Operation was cancelled, no need to throw
        if (folders.Count == 0) return;

        using IStorageItem folder = folders[0];

        var bookmark = await folder.SaveBookmarkAsync();
        _bookmark = bookmark ?? throw new IOException("System denied request to save bookmark.");
    }

    public async Task<IStorageFolder> GetBookmarkedFolderAsync()
    {
        IStorageFolder? folder = await _storageProvider.OpenFolderBookmarkAsync(_bookmark);
        return folder ?? throw new IOException("Can't access bookmarked folder");
    }

    private async Task ReplaceBookmarkAsync(string newBookmark)
    {
        using var folder = (IStorageBookmarkFolder)await GetBookmarkedFolderAsync();
        await folder.ReleaseBookmarkAsync();

        _bookmark = newBookmark;
    }
}

public readonly struct FileExistsResults
{
    public string FileName { get; init; }
    public bool Exists { get; init; }

    public IStorageItem? Item { get; init; }
}

public sealed class StorageFolderWrapper : IDisposable
{
    private readonly IStorageFolder _folder;

    public StorageFolderWrapper(IStorageFolder folder)
    {
        _folder = folder;
    }

    public async Task<FileExistsResults> FileExistsAsync(string fileName, bool saveItemIfFound = false)
        => (await FilesExistAsync(new List<string> {fileName}, saveItemIfFound)).First();

    public async Task<IList<FileExistsResults>> FilesExistAsync(IList<string> fileNames, bool saveItemsIfFound = false)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        var results = new List<FileExistsResults>(fileNames.Count);

        var items = _folder.GetItemsAsync();

        await foreach (IStorageItem item in items)
        {
            var fileName = item.Name;
            var index = fileNames.IndexOf(fileName);

            if (index == -1) continue;

            results[index] = new FileExistsResults
            {
                FileName = fileName,
                Exists = true,
                Item = saveItemsIfFound ? item : null
            };
        }

        return results;
    }

    public async Task<StorageFolderWrapper> GetOrCreateSubFolderAsync(string folderName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        FileExistsResults result = await FileExistsAsync(folderName, saveItemIfFound: true);

        if (!result.Exists) return await CreateSubFolderAsync(folderName);
        if (result.Item is IStorageFolder folder)
        {
            return new StorageFolderWrapper(folder);
        }

        result.Item!.Dispose();
        return await CreateSubFolderAsync(folderName);
    }

    public async Task<StorageFolderWrapper> CreateSubFolderAsync(string folderName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        IStorageFolder newFolder = await _folder.CreateFolderAsync(folderName) ??
                                   throw new IOException("Unable to create new folder");

        return new StorageFolderWrapper(newFolder);
    }

    public async Task<IStorageFile> GetOrCreateFileAsync(string fileName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        FileExistsResults result = await FileExistsAsync(fileName, saveItemIfFound: true);

        if (!result.Exists) return await CreateFileAsync(fileName);
        if (result.Item is IStorageFile file)
        {
            return file;
        }

        result.Item!.Dispose();
        return await CreateFileAsync(fileName);
    }

    public async Task<IStorageFile> CreateFileAsync(string fileName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        IStorageFile? newFile = await _folder.CreateFileAsync(fileName);
        return newFile ?? throw new IOException("Unable to create new file");
    }

    public async Task DeleteFileAsync(string fileName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        FileExistsResults result = await FileExistsAsync(fileName, saveItemIfFound: true);

        if (!result.Exists) return;
        if (result.Item is IStorageFile file)
        {
            await file.DeleteAsync();
            return;
        }

        result.Item!.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _folder.Dispose();
        _disposed = true;
    }

    private bool _disposed;
}