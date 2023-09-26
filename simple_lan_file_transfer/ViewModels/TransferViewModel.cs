using Avalonia.Media;
using MsBox.Avalonia.Enums;
using Avalonia.Platform.Storage;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using simple_lan_file_transfer.Models;

namespace simple_lan_file_transfer.ViewModels;

/// <summary>
/// ViewModel that contains information about a single transfer.
/// </summary>
public sealed partial class TransferViewModel : ViewModelBase, IDisposable
{
    public delegate void SelfRemover(TransferViewModel transferViewModel);

    private bool _disposed;

    private const string FailedText = "Transfer failed";
    private const string FinishedText = "Transfer finished";

    private const string ButtonTextPaused = "Cancel";
    private const string ButtonTextFinished = "Close";

    private static readonly IBrush ProgressBarColorFinished = Brushes.LimeGreen;
    private static readonly IBrush ProgressBarColorFailed = Brushes.Red;
    private static readonly IBrush ProgressBarColorRunning = Brushes.CornflowerBlue;
    private static readonly IBrush ProgressBarColorPaused = Brushes.Gray;

    private readonly FileBlockAccessManager _fileAccessManager;
    private readonly NetworkTransferManagerAsync _networkTransferManager;

    private readonly IStorageFile _transferFile;
    private readonly IStorageFile? _metadataFile;

    private CancellationTokenSource? _pauseTokenSource;

    private SelfRemover? _selfRemover;

    /// <summary>
    /// Whether the transfer is incoming or outgoing
    /// </summary>
    public TransferDirection Direction { get; }

    #region Bound Properties
    public string Name { get; }

    public double FileSizeWithSuffix { get; }

    [ObservableProperty] private bool _showPauseButton;

    [ObservableProperty] private bool _showResumeButton;

    [ObservableProperty] private bool _showCancelButton;

    [ObservableProperty] private string _cancelButtonText = ButtonTextPaused;

    [ObservableProperty] private double _progress;

    [ObservableProperty] private IBrush _progressBarColor = ProgressBarColorRunning;

    private readonly Utility.ByteSuffix _byteSuffix;
    private readonly string _defaultFormatString;

    [ObservableProperty] private string _progressFormatString;

    #endregion

    /// <summary>
    /// Save a delegate used for removing the transfer from the parent collection.
    /// </summary>
    /// <param name="selfRemoverDelegate">The delegate to be saved</param>
    public void RegisterSelfRemover(SelfRemover selfRemoverDelegate) => _selfRemover = selfRemoverDelegate;

    /// <summary>
    /// Start the transfer.
    /// </summary>
    public void RunTransfer()
    {
        _pauseTokenSource = new CancellationTokenSource();

        Task.Run(async () => await StartTransferAsync(_pauseTokenSource.Token), _pauseTokenSource.Token)
            .ContinueWith(
                async x => await OnTransferTaskFinishAsync(x),
                continuationOptions: TaskContinuationOptions.NotOnCanceled)
            .ContinueWith(_ => _pauseTokenSource.Dispose());
    }

    /// <summary>
    /// Dispose of the transfer and remove it from the parent collection.
    /// </summary>
    /// <param name="shouldDispose">Bool indicating whether the Dispose method should be called</param>
    public void RemoveTransfer(bool shouldDispose)
    {
        if (shouldDispose)
        {
            Dispose();
        }
        RemoveTransferFromTab();
    }

    /// <summary>
    /// Pause the transfer and set the UI elements to paused mode.
    /// </summary>
    public void PauseTransfer()
    {
        _pauseTokenSource?.Cancel();
        SetUserInterfaceElementsToPauseMode();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _fileAccessManager.Dispose();
        _networkTransferManager.Dispose();

        _transferFile.Dispose();
        _metadataFile?.Dispose();

        _pauseTokenSource?.Dispose();

        _disposed = true;
    }

    private async Task RemoveMetadataFileAsync()
    {
        if (_metadataFile is null) return;
        await _metadataFile!.DeleteAsync();
    }

    private void RemoveTransferFromTab()
    {
        _selfRemover?.Invoke(this);
    }

    private async Task StartTransferAsync(CancellationToken pauseToken = default)
    {
        SetUserInterfaceElementsToRunningMode();

        if (Direction == TransferDirection.Outgoing)
        {
            var transmitter = new TransmitterTransferManager(_fileAccessManager, _networkTransferManager);
            await transmitter.SendBytesAsync(pauseToken: pauseToken);
        }

        else
        {
            var receiver = new ReceiverTransferManager(_fileAccessManager, _networkTransferManager);
            await receiver.ReceiveBytesAsync(pauseToken: pauseToken);
        }
    }

    private async Task OnTransferTaskFinishAsync(Task transferTask)
    {
        Exception? exception = transferTask.Exception?.InnerExceptions.First();

        if (exception is not null)
        {
            SetUserInterfaceElementsToFailedMode(exception.Message);
        }
        else
        {
            await RemoveMetadataFileAsync();
            SetUserInterfaceElementsToFinishedMode();
        }

        Dispose();
    }

    /// <summary>
    /// Sets up various UI elements. This constructor is private because it should only be called from the factory.
    /// </summary>
    /// <param name="fileAccessManager">File access manager to use for the transfer</param>
    /// <param name="networkTransferManager">Network transfer manager used for the transfer</param>
    /// <param name="direction">Direction of the transfer</param>
    /// <param name="transferFile">File used in the transfer</param>
    /// <param name="metadataFile">Metadata file used in the transfer</param>
    private TransferViewModel(FileBlockAccessManager fileAccessManager, NetworkTransferManagerAsync networkTransferManager,
        TransferDirection direction, IStorageFile transferFile, IStorageFile? metadataFile = null)
    {
        _fileAccessManager = fileAccessManager;
        _networkTransferManager = networkTransferManager;
        Direction = direction;
        _transferFile = transferFile;
        _metadataFile = metadataFile;

        Name = transferFile.Name;

        _fileAccessManager.PropertyChanged += OnProgressChanged;

        _byteSuffix = Utility.GetHighestPossibleByteSuffixForNumber(_fileAccessManager.FileSize);
        var dividedFileSize = Utility.DivideNumberToFitSuffix(_fileAccessManager.FileSize, _byteSuffix);

        _defaultFormatString =
            $"{{0:F2}} {_byteSuffix} / {dividedFileSize:F2} {_byteSuffix}";

        FileSizeWithSuffix = dividedFileSize;

        _progressFormatString = _defaultFormatString;

        RunTransfer();
    }

    private void OnProgressChanged(object? sender, EventArgs e)
    {
        var args = e as PropertyChangedEventArgs;
        if (args?.PropertyName != nameof(_fileAccessManager.LastProcessedBlock)) return;

        Progress = CalculateProgress();
    }

    private double CalculateProgress()
    {
        var progress = _fileAccessManager.LastProcessedBlock * Utility.BlockSize;
        return Utility.DivideNumberToFitSuffix(progress, _byteSuffix);
    }

    private void SetUserInterfaceElementsToRunningMode()
    {
        ShowPauseButton = true;
        ShowCancelButton = false;
        ShowResumeButton = false;

        ProgressFormatString = _defaultFormatString;
        ProgressBarColor = ProgressBarColorRunning;
    }

    private void SetUserInterfaceElementsToPauseMode()
    {
        ShowPauseButton = false;
        ShowCancelButton = true;
        ShowResumeButton = true;

        ProgressFormatString = $"{_defaultFormatString} (Paused)";
        ProgressBarColor = ProgressBarColorPaused;
    }

    private void SetUserInterfaceElementsToFailedMode(string failMessage)
    {
        ShowPauseButton = false;
        ShowCancelButton = true;
        ShowResumeButton = false;

        ProgressFormatString = $"{FailedText} ({failMessage})";
        ProgressBarColor = ProgressBarColorFailed;
        CancelButtonText = ButtonTextFinished;
    }

    private void SetUserInterfaceElementsToFinishedMode()
    {
        ShowPauseButton = false;
        ShowCancelButton = true;
        ShowResumeButton = false;

        ProgressFormatString = FinishedText;
        ProgressBarColor = ProgressBarColorFinished;
        CancelButtonText = ButtonTextFinished;
    }
}

#region TransferViewModelFactory
public partial class TransferViewModel
{
    public enum TransferDirection
    {
        Incoming,
        Outgoing
    }

    /// <summary>
    /// Factory that takes care of creating new <see cref="TransferViewModel"/>s. This includes creating the underlying
    /// transfer and communicating the transfer parameters.
    /// </summary>
    public static class TransferViewModelAsyncFactory
    {
        /// <summary>
        /// Creates new outgoing transfer and a corresponding transfer view model. This includes creating the underlying
        /// transfer and communicating the transfer parameters.
        /// </summary>
        /// <param name="socket">Socket which is connected to a remote receiver</param>
        /// <param name="file">The file to send</param>
        /// <param name="password">The password of the receiver</param>
        /// <param name="cancellationToken"/>
        /// <returns>Transfer view model containing the resultant transfer</returns>
        /// <exception cref="IOException">Thrown when the file doesn't support reading file size</exception>
        public static async Task<TransferViewModel> CreateOutgoingTransferViewModelAsync(Socket socket,
            IStorageFile file, string password, CancellationToken cancellationToken = default)
        {
            NetworkTransferManagerAsync networkTransferManager = new (socket);
            SenderParameterCommunicationManager parameterCommunicationManager = new (networkTransferManager);

            try
            {
                await parameterCommunicationManager.SendPassword(password, cancellationToken);
            }
            catch (Exception)
            {
                networkTransferManager.Dispose();
                throw;
            }

            Stream fileStream = await file.OpenReadAsync();

            var fileSize = (await file.GetBasicPropertiesAsync()).Size
                           ?? throw new IOException("File size is inaccessible");

            var fileSizeSigned = (int)fileSize;

            FileBlockAccessManager fileAccessManager = new (fileStream, fileSizeSigned);

            var fileName = file.Name;

            // We are using MD5 because it is not used for security purposes, but rather for file integrity.
            using HashAlgorithm hashAlgorithm = MD5.Create();
            var hash = await Task.Run(
                async () => await hashAlgorithm.ComputeHashAsync(fileStream, cancellationToken)
                , cancellationToken);

            int lastWrittenBlock;
            try
            {
                var metadata = new FileMetadata
                {
                    Name = fileName,
                    Hash = hash,
                    Size = fileSizeSigned
                };

                await parameterCommunicationManager.SendMetadataAsync(metadata, cancellationToken);
                lastWrittenBlock = await parameterCommunicationManager.ReceiveLastWrittenBlock(cancellationToken);
            }
            catch (Exception)
            {
                fileAccessManager.Dispose();
                networkTransferManager.Dispose();
                throw;
            }

            fileAccessManager.SeekToBlock(lastWrittenBlock);

            return new TransferViewModel(
                fileAccessManager,
                networkTransferManager,
                TransferDirection.Outgoing,
                file);
        }

        /// <summary>
        /// Creates new incoming transfer and a corresponding transfer view model. This includes creating the underlying
        /// transfer and communicating the transfer parameters.
        /// </summary>
        /// <param name="socket">Socket connected to the remote sender</param>
        /// <param name="rootFolder">Folder where the file will be saved</param>
        /// <param name="metadataFolderName">Name of the folder where metadata is stored</param>
        /// <param name="password">Expected password, empty if none</param>
        /// <param name="cancellationToken"/>
        /// <returns>Transfer view model with the resultant incoming transfer</returns>
        public static async Task<TransferViewModel> CreateIncomingTransferViewModelAsync(
            Socket socket,
            StorageFolderWrapper rootFolder,
            string metadataFolderName,
            string password,
            CancellationToken cancellationToken = default)
        {
            NetworkTransferManagerAsync networkTransferManager = new (socket);
            ReceiverParameterCommunicationManager parameterCommunicationManager = new (networkTransferManager);

            await parameterCommunicationManager.ReceivePassword(password, cancellationToken);

            var (receivedFileName, hash, fileSize) =
                await parameterCommunicationManager.ReceiveMetadataAsync(cancellationToken);

            await AskForTransferConfirmationAsync(receivedFileName, socket.RemoteEndPoint!.ToString()!);

            var metadataFileName = Convert.ToHexString(hash.Span);

            FileCarrier result = await GetCorrectMetadataAndDataFileAsync(
                rootFolder,
                metadataFolderName,
                receivedFileName,
                metadataFileName);
            (IStorageFile metadataFile, IStorageFile file, var lastWrittenBlock) = result;

            Stream fileStream = await file.OpenWriteAsync();

            Stream metadataStream = await metadataFile.OpenWriteAsync();

            MetadataWriter? metadataWriter;
            if (!metadataStream.CanSeek)
            {
                // If we can't seek, that means we can't seek to the last written block, so we have to start from the
                // beginning. We also can't save our progress

                lastWrittenBlock = 0;
                metadataWriter = null;
                metadataStream.Dispose();
                metadataFile.Dispose();
            }
            else
            {
                metadataWriter = new MetadataWriter(metadataStream);
                metadataWriter.WriteFileName(file.Name);
            }

            var fileAccessManager = new FileBlockAccessManager(fileStream, fileSize, metadataWriter);
            fileAccessManager.SeekToBlock(lastWrittenBlock);

            await parameterCommunicationManager.SendLastWrittenBlockAsync(lastWrittenBlock, cancellationToken);

            return new TransferViewModel(
                fileAccessManager,
                networkTransferManager,
                TransferDirection.Incoming,
                file,
                metadataFile);
        }

        /// <summary>
        /// Returns the correct metadata and data file for the transfer. If the file already exists, but the metadata
        /// doesn't, the user will be prompted if they want to overwrite the file. If not, a new file name will be
        /// created by adding (n) to the end of the filename.
        /// </summary>
        /// <param name="rootFolder">Folder where the file will be saved</param>
        /// <param name="metadataFolderName">Folder where metadata is saved</param>
        /// <param name="receivedFileName">File name that was received from the sender</param>
        /// <param name="metadataFileName">Metadata file name</param>
        /// <returns></returns>
        private static async Task<FileCarrier> GetCorrectMetadataAndDataFileAsync(
            StorageFolderWrapper rootFolder, string metadataFolderName, string receivedFileName, string metadataFileName)
        {
            StorageFolderWrapper metadataFolderProvider = await rootFolder.GetOrCreateSubFolderAsync(metadataFolderName);
            IStorageFile metadataFile = await metadataFolderProvider.GetOrCreateFileAsync(metadataFileName);

            StorageItemProperties result = await metadataFile.GetBasicPropertiesAsync();

            // If the file has just been created, then the file size is 0 so we can use this to tell if it existed or not
            var metadataFileExisted = result.Size > 0;

            var actualFileName = receivedFileName;

            var lastWrittenBlock = 0;

            if (metadataFileExisted)
            {
                using var metadataReader = new MetadataReader(await metadataFile.OpenReadAsync());
                lastWrittenBlock = metadataReader.ReadFileLastWrittenBlock();
                actualFileName = metadataReader.ReadFileName();

                FileExistsResult savedFileExists = await rootFolder.FileExistsAsync(actualFileName, saveItemIfFound: true);
                if (savedFileExists is { Exists: true, Item: IStorageFile file })
                {
                    return new FileCarrier
                    {
                        MetadataFile = metadataFile,
                        TransferFile = file,
                        WrittenBlocksCount = lastWrittenBlock
                    };
                }

                savedFileExists.Item?.Dispose();
            }

            actualFileName = await CheckForAndHandleFileConflicts(rootFolder, actualFileName);
            return new FileCarrier
            {
                MetadataFile = metadataFile,
                TransferFile = await rootFolder.GetOrCreateFileAsync(actualFileName),
                WrittenBlocksCount = lastWrittenBlock
            };
        }

        /// <summary>
        /// Checks if a file exists and if so, asks the user if they want to overwrite it. If not, a new file name will
        /// be created with a number at the end.
        /// </summary>
        /// <param name="folder">Folder where to search in</param>
        /// <param name="originalFileName">File name to check</param>
        /// <returns>An available file name</returns>
        /// <exception cref="LocalTransferCancelledException">
        /// Thrown if the user decides to cancel the transfer
        /// </exception>
        /// <exception cref="IndexOutOfRangeException">Thrown if the ButtonResult value was invalid</exception>
        private static async Task<string> CheckForAndHandleFileConflicts(StorageFolderWrapper folder, string originalFileName)
        {
            FileExistsResult result = await folder.FileExistsAsync(originalFileName);
            if (!result.Exists) return originalFileName;

            ButtonResult messageBoxResult = await ShowFileConflictMessageBoxAsync(originalFileName);
            switch (messageBoxResult)
            {
                case ButtonResult.Yes:
                    await folder.DeleteFileAsync(originalFileName);
                    return originalFileName;
                case ButtonResult.No:
                    return await GetUniqueFileNameAsync(folder, originalFileName);
                case ButtonResult.Abort:
                    throw new LocalTransferCancelledException("Transfer was cancelled by user");
                case ButtonResult.Cancel or ButtonResult.Ok or ButtonResult.None:
                    goto default;
                default:
                    throw new IndexOutOfRangeException("Invalid ButtonResult value");
            }
        }

        /// <summary>
        /// Gets a unique file name by adding (n) to the end of the file name. Returns first available file name of this
        /// type.
        /// </summary>
        /// <param name="folder">Folder to search in</param>
        /// <param name="fileName">File name to check</param>
        /// <returns></returns>
        private static async Task<string> GetUniqueFileNameAsync(StorageFolderWrapper folder, string fileName)
        {
            const int batchSize = 5;

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);


            // Testing in batches to not waste FileExistsAsync calls
            List<string> filenamesToTest = new(batchSize);
            var start = 1;
            for (;;)
            {
                for (var i = start; i < batchSize + start; ++i)
                {
                    filenamesToTest.Add(fileNameWithoutExtension + $" ({i})" + extension);
                }

                var results = await folder.FilesExistAsync(filenamesToTest);

                FileExistsResult? available = results
                    .OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(x => !x.Exists);

                if (available is not null) return available.Value.FileName;

                start += batchSize;
            }
        }

        /// <summary>
        /// Asks user if they want to receive the file. If not, throws <see cref="LocalTransferCancelledException"/>.
        /// </summary>
        /// <param name="fileName">File name to include in question</param>
        /// <param name="ip">Address of remote sender</param>
        /// <exception cref="LocalTransferCancelledException">Thrown if user cancels the transfer</exception>
        private static async Task AskForTransferConfirmationAsync(string fileName, string ip)
        {
            const string title = "Transfer confirmation";
            var message = $"Do you want to receive {fileName} from {ip}?";

            ButtonResult result = await ShowPopup(message, title, ButtonEnum.YesNo, Icon.Info);

            if (result == ButtonResult.No)
            {
                throw new LocalTransferCancelledException("Transfer was cancelled by user");
            }
        }

        /// <summary>
        /// Asks the user what they want to do about a file conflict.
        /// </summary>
        /// <param name="fileName">Name of the conflicting file</param>
        /// <returns>Which button the user pressed</returns>
        private static async Task<ButtonResult> ShowFileConflictMessageBoxAsync(string fileName)
        {
            const string title = "File already exists";
            var message = fileName + " already exists. Do you want to overwrite it?";

            return await ShowPopup(message, title, ButtonEnum.YesNoAbort, Icon.Info);
        }

        private record struct FileCarrier(IStorageFile MetadataFile, IStorageFile TransferFile, int WrittenBlocksCount);
    }
}
#endregion