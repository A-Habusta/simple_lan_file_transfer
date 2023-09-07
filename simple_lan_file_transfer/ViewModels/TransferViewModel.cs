using ReactiveUI;
using Avalonia.Media;
using MsBox.Avalonia.Enums;
using Avalonia.Platform.Storage;
using System.Security.Cryptography;
using simple_lan_file_transfer.Models;

namespace simple_lan_file_transfer.ViewModels;

public partial class TransferViewModel : ViewModelBase
{
    private static readonly IBrush ProgressBarColorRunning = Brushes.CornflowerBlue;
    private static readonly IBrush ProgressBarColorPaused = Brushes.Gray;

    public delegate void SelfRemover(TransferViewModel transferViewModel);

    private readonly FileBlockAccessManager _fileAccessManager;
    private readonly NetworkTransferManagerAsync _networkTransferManager;

    private readonly IStorageFile _transferFile;
    private readonly IStorageFile? _metadataFile;

    private CancellationTokenSource? _pauseTokenSource;

    private SelfRemover? _selfRemover;
    public TransferDirection Direction { get; }

    #region Bound Properties
    public string Name => _transferFile.Name;

    public double FileSizeWithSuffix { get; }

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set => this.RaiseAndSetIfChanged(ref _isPaused, value);
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    private IBrush _progressBarColor = ProgressBarColorRunning;
    public IBrush ProgressBarColor
    {
        get => _progressBarColor;
        set => this.RaiseAndSetIfChanged(ref _progressBarColor, value);
    }

    private readonly Utility.ByteSuffix _byteSuffix;
    private readonly string _defaultFormatString;
    private string _progressFormatString;
    public string ProgressFormatString
    {
        get => _progressFormatString;
        set => this.RaiseAndSetIfChanged(ref _progressFormatString, value);
    }

    #endregion

    public void RegisterSelfRemover(SelfRemover selfRemoverDelegate) => _selfRemover = selfRemoverDelegate;

    public void TerminateTransfer()
    {
        RemoveTransferFromTab();
    }

    public void PauseTransfer()
    {
        _pauseTokenSource?.Cancel();
        SetUserInterfaceElementsToPauseMode();
    }

    public void RemoveTransferFromTab()
    {
        _selfRemover?.Invoke(this);
        _fileAccessManager.Dispose();
        _networkTransferManager.Dispose();

        _transferFile.Dispose();
        _metadataFile?.Dispose();

        _pauseTokenSource?.Dispose();
    }

    public async Task StartTransferAsync()
    {
        _pauseTokenSource = new CancellationTokenSource();
        CancellationToken pauseToken = _pauseTokenSource.Token;

        ResetUserInterfaceElements();

        try
        {
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
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException
                                       or InvalidPasswordException or RemoteTransferCancelledException)
        {
            await ShowPopup(ex.Message);
        }

        await OnTransferFinishAsync();
    }

    private async Task OnTransferFinishAsync()
    {
        await RemoveMetadataFileAsync();
        RemoveTransferFromTab();
    }

    private async Task RemoveMetadataFileAsync()
    {
        if (_metadataFile is null) return;
        await _metadataFile!.DeleteAsync();
    }

    private TransferViewModel(FileBlockAccessManager fileAccessManager, NetworkTransferManagerAsync networkTransferManager,
        TransferDirection direction, IStorageFile transferFile, IStorageFile? metadataFile = null)
    {
        _fileAccessManager = fileAccessManager;
        _networkTransferManager = networkTransferManager;
        Direction = direction;
        _transferFile = transferFile;
        _metadataFile = metadataFile;

        _fileAccessManager.PropertyChanged += OnProgressChanged;

        _byteSuffix = Utility.GetHighestPossibleByteSuffixForNumber(_fileAccessManager.FileSize);
        FileSizeWithSuffix = Utility.DivideNumberToFitSuffix(_fileAccessManager.FileSize, _byteSuffix);

        _defaultFormatString =
            $"{{0}} {_byteSuffix} / {FileSizeWithSuffix} {_byteSuffix}";

        _progressFormatString = _defaultFormatString;
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

    private void ResetUserInterfaceElements()
    {
        IsPaused = false;
        ProgressBarColor = ProgressBarColorRunning;
        ProgressFormatString = _defaultFormatString;
    }

    private void SetUserInterfaceElementsToPauseMode()
    {
        IsPaused = true;
        ProgressBarColor = ProgressBarColorPaused;
        AddPauseToProgressText();
    }

    private void AddPauseToProgressText()
    {
        ProgressFormatString = $"{_defaultFormatString} (Paused)";
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

    public static class TransferViewModelAsyncFactory
    {
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

            var fileSizeSigned = (long)fileSize;

            FileBlockAccessManager fileAccessManager = new (fileStream, fileSizeSigned);

            var fileName = file.Name;

            // We are using MD5 because it is not used for security purposes, but rather for file integrity.
            using HashAlgorithm hashAlgorithm = MD5.Create();
            var hash = await Task.Run(
                async () => await hashAlgorithm.ComputeHashAsync(fileStream, cancellationToken)
                , cancellationToken);

            long lastWrittenBlock;
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

            var (receivedFileName, hash, fileSize) = await parameterCommunicationManager.ReceiveMetadataAsync(cancellationToken);
            await AskForTransferConfirmationAsync(receivedFileName, socket.RemoteEndPoint!.ToString()!);

            var metadataFileName = BitConverter.ToString(hash);

            FileCarrier result = await GetCorrectMetadataAndDataFileAsync(rootFolder,
                metadataFolderName,
                receivedFileName,
                metadataFileName);
            (IStorageFile metadataFile, IStorageFile file, var lastWrittenBlock) = result;

            Stream fileStream = await file.OpenWriteAsync();

            Stream metadataStream = await metadataFile.OpenWriteAsync();
            var metadataHandler = new MetadataWriter(metadataStream);

            var fileAccessManager = new FileBlockAccessManager(fileStream, fileSize, metadataHandler);
            fileAccessManager.SeekToBlock(lastWrittenBlock);

            return new TransferViewModel(
                fileAccessManager,
                networkTransferManager,
                TransferDirection.Incoming,
                file,
                metadataFile);
        }

        private static async Task<FileCarrier> GetCorrectMetadataAndDataFileAsync(
            StorageFolderWrapper rootFolder, string metadataFolderName, string receivedFileName, string metadataFileName)
        {
            StorageFolderWrapper metadataFolderProvider = await rootFolder.GetOrCreateSubFolderAsync(metadataFolderName);
            IStorageFile metadataFile = await metadataFolderProvider.GetOrCreateFileAsync(metadataFileName);

            StorageItemProperties result = await metadataFile.GetBasicPropertiesAsync();
            var metadataFileExists = result.Size > sizeof(long);

            var actualFileName = receivedFileName;

            long lastWrittenBlock = 0;

            if (metadataFileExists)
            {
                using var metadataReader = new MetadataReader(await metadataFile.OpenReadAsync());
                lastWrittenBlock = metadataReader.ReadFileLastWrittenBlock();
                actualFileName = metadataReader.ReadFileName();
            }

            actualFileName = await CheckForAndHandleFileConflicts(rootFolder, actualFileName);
            return new FileCarrier(metadataFile, await rootFolder.GetOrCreateFileAsync(actualFileName), lastWrittenBlock);
        }

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

        private static async Task<ButtonResult> ShowFileConflictMessageBoxAsync(string fileName)
        {
            const string title = "File already exists";
            var message = fileName + " already exists. Do you want to overwrite it?";

            return await ShowPopup(message, title, ButtonEnum.YesNoAbort, Icon.Info);
        }

        private record struct FileCarrier(IStorageFile MetadataFile, IStorageFile TransferFile, long ReadBlocksCount);
    }
}
#endregion