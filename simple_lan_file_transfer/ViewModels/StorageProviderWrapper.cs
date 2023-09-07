using Avalonia.Platform.Storage;

namespace simple_lan_file_transfer.ViewModels;

public class StorageProviderWrapper
{
    private readonly IStorageProvider _storageProvider;
    private string? _bookmark;


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

        var bookmark = await folder.SaveBookmarkAsync() ?? throw new IOException("Unable to save bookmark");
        await SaveNewBookmarkAsync(bookmark);
    }

    public async Task<IStorageFolder> GetBookmarkedFolderAsync()
    {
        if (_bookmark is null) await PickNewBookmarkedFolderAsync("Pick a folder to save files to");

        IStorageFolder? folder = await _storageProvider.OpenFolderBookmarkAsync(_bookmark!);
        return folder ?? throw new IOException("Can't access bookmarked folder");
    }

    private async Task SaveNewBookmarkAsync(string newBookmark)
    {
        if (_bookmark is not null)
        {
            using var oldFolder = (IStorageBookmarkFolder)await GetBookmarkedFolderAsync();
            await oldFolder.ReleaseBookmarkAsync();
        }

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
        => (await FilesExistAsync(new List<string> {fileName}, saveItemIfFound)).FirstOrDefault();

    public async Task<IList<FileExistsResults>> FilesExistAsync(IList<string> fileNames, bool saveItemsIfFound = false)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));


        var results = new List<FileExistsResults>(fileNames.Count);

        var items = _folder.GetItemsAsync();

        await foreach (IStorageItem storageItem in items)
        {
            var index = fileNames.IndexOf(storageItem.Name);
            if (index == -1)
            {
                storageItem.Dispose();
                continue;
            }

            results.Add(new FileExistsResults
            {
                FileName = storageItem.Name,
                Exists = true,
                Item = saveItemsIfFound ? storageItem : null
            });
            if (!saveItemsIfFound) storageItem.Dispose();

            fileNames.RemoveAt(index);
        }

        results.AddRange(fileNames.Select(fileName => new FileExistsResults { FileName = fileName, Exists = false, Item = null }));
        return results;
    }

    public async Task<StorageFolderWrapper> GetOrCreateSubFolderAsync(string folderName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        FileExistsResults result = await FileExistsAsync(folderName, saveItemIfFound: true);

        if (!result.Exists)
        {
            return await CreateSubFolderAsync(folderName);
        }
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