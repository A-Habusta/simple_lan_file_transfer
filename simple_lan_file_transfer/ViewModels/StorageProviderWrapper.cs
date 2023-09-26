using Avalonia.Platform.Storage;

namespace simple_lan_file_transfer.ViewModels;

/// <summary>
/// Class that wraps the <see cref="IStorageProvider"/> and provides a more convenient interface for the application.
/// </summary>
public class StorageProviderWrapper
{
    private readonly IStorageProvider _storageProvider;
    private string? _bookmark;


    public StorageProviderWrapper(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }

    /// <summary>
    /// Opens a file picker and returns the selected files.
    /// </summary>
    /// <param name="pickerTitle">Title of the file picker</param>
    /// <returns>A collection containing the selected files (empty if none)</returns>
    public async Task<IEnumerable<IStorageFile>> PickFilesAsync(string pickerTitle)
    {
        var pickerOptions = new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = pickerTitle
        };

        return await _storageProvider.OpenFilePickerAsync(pickerOptions);
    }

    /// <summary>
    /// Opens a folder picker and saves the selected folder as a bookmark. This is necessary to do in order to keep
    /// folder access in android.
    /// </summary>
    /// <param name="pickerTitle">Title of the picker</param>
    /// <exception cref="IOException">Thrown when saving the bookmark failed</exception>
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

    /// <summary>
    /// Opens the bookmarked folder, picks a new one if none is bookmarked.
    /// </summary>
    /// <returns>Bookmarked folder</returns>
    /// <exception cref="IOException">Thrown when the operation fails</exception>
    public async Task<IStorageFolder> GetBookmarkedFolderAsync()
    {
        if (_bookmark is null) await PickNewBookmarkedFolderAsync("Pick a folder to save files to");

        // If we didn't save a new bookmark anyway then we can't access the folder
        if (_bookmark is null) throw new IOException("No folder specified");

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

/// <summary>
/// Struct containing the results of a FileExistsAsync operation.
/// </summary>
public readonly struct FileExistsResult
{
    public string FileName { get; init; }
    public bool Exists { get; init; }

    public IStorageItem? Item { get; init; }
}

/// <summary>
/// Wraps the <see cref="IStorageFolder"/> and provides a more convenient interface for the application.
/// </summary>
public sealed class StorageFolderWrapper : IDisposable
{
    private readonly IStorageFolder _folder;

    public StorageFolderWrapper(IStorageFolder folder)
    {
        _folder = folder;
    }

    /// <summary>
    /// Checks if the file exists in the folder. The user can choose to save the item if it exists.
    /// </summary>
    /// <param name="fileName">Name of the checked file</param>
    /// <param name="saveItemIfFound">Whether to save the file if it exists</param>
    /// <returns>
    /// A struct which contains the original filename, whether the file exists or not, and the file itself if the user
    /// requested it
    /// </returns>
    public async Task<FileExistsResult> FileExistsAsync(string fileName, bool saveItemIfFound = false)
        => (await FilesExistAsync(new List<string> {fileName}, saveItemIfFound)).FirstOrDefault();

    /// <summary>
    /// Checks if the files exist in the folder. The user can choose to save the items if they exist.
    /// </summary>
    /// <param name="fileNames">Collection of file names to check</param>
    /// <param name="saveItemsIfFound">Specifie whether files should be saved if they exist</param>
    /// <returns>
    /// Collection of structs which contain the original filename, whether the corresponding file exists or not, and the
    /// files themselves if the user requested them
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object is disposed</exception>
    public async Task<IList<FileExistsResult>> FilesExistAsync(IList<string> fileNames, bool saveItemsIfFound = false)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));


        var results = new List<FileExistsResult>(fileNames.Count);

        var items = _folder.GetItemsAsync();

        await foreach (IStorageItem storageItem in items)
        {
            var index = fileNames.IndexOf(storageItem.Name);
            if (index == -1)
            {
                storageItem.Dispose();
                continue;
            }

            results.Add(new FileExistsResult
            {
                FileName = storageItem.Name,
                Exists = true,
                Item = saveItemsIfFound ? storageItem : null
            });
            if (!saveItemsIfFound) storageItem.Dispose();

            fileNames.RemoveAt(index);
        }

        results.AddRange(fileNames.Select(fileName => new FileExistsResult { FileName = fileName, Exists = false, Item = null }));
        return results;
    }

    /// <summary>
    /// Finds the specified subfolder in the folder. If it doesn't exist, it will be created.
    /// </summary>
    /// <param name="folderName">Target folder name</param>
    /// <returns>A wrapper around the target folder</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the object is disposed</exception>
    public async Task<StorageFolderWrapper> GetOrCreateSubFolderAsync(string folderName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        FileExistsResult result = await FileExistsAsync(folderName, saveItemIfFound: true);

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

    /// <summary>
    /// Creates a new subfolder in this folder.
    /// </summary>
    /// <param name="folderName">Name of the folder to be created</param>
    /// <returns>A wrapper around the created folder</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the object is disposed</exception>
    /// <exception cref="IOException">Thrown if the creation failed</exception>
    public async Task<StorageFolderWrapper> CreateSubFolderAsync(string folderName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        IStorageFolder newFolder = await _folder.CreateFolderAsync(folderName) ??
                                   throw new IOException("Unable to create new folder");

        return new StorageFolderWrapper(newFolder);
    }

    /// <summary>
    /// Gets the specified file from the folder. If it doesn't exist, it will be created.
    /// </summary>
    /// <param name="fileName">Target filename</param>
    /// <returns>The file that was opened or created</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the object is disposed</exception>
    public async Task<IStorageFile> GetOrCreateFileAsync(string fileName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        FileExistsResult result = await FileExistsAsync(fileName, saveItemIfFound: true);

        if (!result.Exists) return await CreateFileAsync(fileName);
        if (result.Item is IStorageFile file)
        {
            return file;
        }

        result.Item!.Dispose();
        return await CreateFileAsync(fileName);
    }

    /// <summary>
    /// Creates a new file in this folder.
    /// </summary>
    /// <param name="fileName">Target file name</param>
    /// <returns>The new file</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the object is disposed</exception>
    /// <exception cref="IOException">Thrown if the creation failed</exception>
    public async Task<IStorageFile> CreateFileAsync(string fileName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        IStorageFile? newFile = await _folder.CreateFileAsync(fileName);
        return newFile ?? throw new IOException("Unable to create new file");
    }

    /// <summary>
    /// Deletes the specified file from the folder
    /// </summary>
    /// <param name="fileName">Name of to be deleted</param>
    /// <exception cref="ObjectDisposedException">Thrown if the object is deleted</exception>
    public async Task DeleteFileAsync(string fileName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StorageFolderWrapper));

        FileExistsResult result = await FileExistsAsync(fileName, saveItemIfFound: true);

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