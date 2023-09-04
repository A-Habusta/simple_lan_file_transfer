using System.Text;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Security.Cryptography;
using simple_lan_file_transfer.Models;

namespace simple_lan_file_transfer.ViewModels;

public partial class TransferViewModel
{
    public delegate void SelfRemover(TransferViewModel transferViewModel);

    private readonly FileBlockAccessManager _fileAccessManager;
    private readonly NetworkTransferManagerAsync _transferManager;
    private SelfRemover? _selfRemover;
    public TransferDirection Direction { get; }

    public void RegisterSelfRemover(SelfRemover selfRemoverDelegate) => _selfRemover = selfRemoverDelegate;

    public void RemoveTransferFromTab()
    {
        _selfRemover?.Invoke(this);
        _fileAccessManager.Dispose();
        _transferManager.Dispose();
    }

    private TransferViewModel(FileBlockAccessManager fileAccessManager, NetworkTransferManagerAsync transferManager,
        TransferDirection direction)
    {
        _fileAccessManager = fileAccessManager;
        _transferManager = transferManager;
        Direction = direction;
    }
}

public partial class TransferViewModel
{
    public enum TransferDirection
    {
        Incoming,
        Outgoing
    }

    public static class TransferViewModelAsyncFactory
    {
        public static async Task<TransferViewModel> CreateOutgoingTransferViewModelAsync(Socket socket, string filePath,
            CancellationToken cancellationToken = default)
        {
            FileStream fileStream = new (filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            FileBlockAccessManager fileAccessManager = new (fileStream);
            NetworkTransferManagerAsync networkTransferManager = new (socket);
            SenderParameterCommunicationManager parameterCommunicationManager = new (networkTransferManager);

            var fileName = Path.GetFileName(filePath);

            // We are using MD5 because it is not used for security purposes, but rather for file integrity.
            using HashAlgorithm hashAlgorithm = MD5.Create();
            var hash = await Task.Run(
                async () => await hashAlgorithm.ComputeHashAsync(fileStream, cancellationToken)
                , cancellationToken);

            long lastWrittenBlock;
            try
            {
                await parameterCommunicationManager.SendMetadataAsync(fileName, hash, cancellationToken);
                lastWrittenBlock = await parameterCommunicationManager.ReceiveLastWrittenBlock(cancellationToken);
            }
            catch (Exception)
            {
                fileAccessManager.Dispose();
                networkTransferManager.Dispose();
                throw;
            }

            fileAccessManager.SeekToBlock(lastWrittenBlock);

            return new TransferViewModel(fileAccessManager, networkTransferManager, TransferDirection.Outgoing);
        }

        public static async Task<TransferViewModel> CreateIncomingTransferViewModelAsync(Socket socket,
            string receiveDirectoryPath, string metadataDirectoryPath, CancellationToken cancellationToken = default)
        {
            NetworkTransferManagerAsync networkTransferManager = new (socket);
            ReceiverParameterCommunicationManager parameterCommunicationManager = new (networkTransferManager);

            var (fileName, hash) = await parameterCommunicationManager.ReceiveMetadataAsync(cancellationToken);

            var metadataFile = BitConverter.ToString(hash);

            (MetadataHandler metadataHandler, var actualFilePath) = await GetCorrectMetadataAndDataFileAsync(
                receiveDirectoryPath, metadataDirectoryPath, fileName, metadataFile);

            FileStream fileStream = new (actualFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

            var fileAccessManager = new FileBlockAccessManager(fileStream, metadataHandler);
            var lastWrittenBlock = metadataHandler.ReadFileLastWrittenBlock();
            fileAccessManager.SeekToBlock(lastWrittenBlock);

            return new TransferViewModel(fileAccessManager, networkTransferManager, TransferDirection.Incoming);
        }

        private static async Task<ButtonResult> ShowFileConflictMessageBoxAsync(string fileName)
        {
            const string title = "File already exists";
            var message = fileName + " already exists. Do you want to overwrite it?";

            var messageBoxManager = MessageBoxManager.GetMessageBoxStandard(
                title,
                message,
                ButtonEnum.YesNoAbort,
                Icon.Warning);

            return await messageBoxManager.ShowAsync();
        }

        private static async Task<(MetadataHandler metadataHandler, string actualFilePath)> GetCorrectMetadataAndDataFileAsync(
            string rootDirectoryPath, string metadataDirectoryPath, string receivedFileName, string metadataFileName)
        {
            var metadataFilePath = Path.Combine(metadataDirectoryPath, metadataFileName);
            var receivedFileNamePath = Path.Combine(receivedFileName, rootDirectoryPath);


            var metadataFileExists = File.Exists(metadataFilePath);
            var receivedFileNameExists = File.Exists(receivedFileNamePath);

            MetadataHandler metadataHandler = MetadataHandler.MetadataHandlerFactory.CreateMetadataHandler(metadataFilePath);

            if (metadataFileExists)
            {
                var savedFileName = metadataHandler.ReadFileName();
                var savedFilePath = Path.Combine(savedFileName, rootDirectoryPath);
                var savedFileExists = File.Exists(savedFilePath);

                if (savedFileExists)
                {
                    return (metadataHandler, savedFilePath);
                }

                metadataHandler.WriteLastBlockProcessed(0);
            }

            if (receivedFileNameExists)
            {
                ButtonResult overwriteChoiceResults = await ShowFileConflictMessageBoxAsync(receivedFileName);
                switch (overwriteChoiceResults)
                {
                    case ButtonResult.Yes:
                        break;
                    case ButtonResult.No:
                        receivedFileName = GetUniqueFileName(receivedFileNamePath);
                        break;
                    case ButtonResult.Abort:
                        throw new LocalTransferCancelledException("Transfer was cancelled by user");

                    case ButtonResult.Ok or ButtonResult.Cancel or ButtonResult.None:
                        goto default;
                    default:
                        throw new ArgumentOutOfRangeException(overwriteChoiceResults.ToString());
                }
            }

            metadataHandler.WriteFileName(receivedFileName);
            return (metadataHandler, receivedFileNamePath);
        }

        private static string GetUniqueFileName(string filePath)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

            var counter = 1;
            string fullFileName;

            do
            {
                fullFileName = fileNameWithoutExtension + $" ({counter})" + extension;
                filePath = Path.Combine(directory, fullFileName);
                counter++;
            }
            while (File.Exists(filePath));

            return fullFileName;
        }
    }
}