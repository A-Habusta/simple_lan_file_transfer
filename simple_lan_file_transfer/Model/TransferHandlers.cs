using System.Text;

namespace simple_lan_file_transfer.Models;

public readonly struct TransmitterTransferManager
{
    private readonly IBlockSequentialReader _blockReader;
    private readonly IByteSenderAsync _byteSenderAsync;

    public TransmitterTransferManager(IBlockSequentialReader blockReader, IByteSenderAsync byteSenderAsync)
    {
        _blockReader = blockReader;
        _byteSenderAsync = byteSenderAsync;
    }

    public async Task SendBytesAsync(CancellationToken cancellationToken = default, CancellationToken pauseToken = default)
    {
        for (;;)
        {
            pauseToken.ThrowIfCancellationRequested();

            var block = _blockReader.ReadNextBlock();
            cancellationToken.ThrowIfCancellationRequested();

            if (block.Length == 0)
            {
                await _byteSenderAsync.SendAsync(MessageType.EndOfTransfer, cancellationToken: cancellationToken);
                return;
            }


            await _byteSenderAsync.SendAsync(MessageType.Data, block, cancellationToken);

            if (block.Length == Utility.BlockSize) continue;

            await _byteSenderAsync.SendAsync(MessageType.EndOfTransfer, cancellationToken: cancellationToken);
            return;
        }
    }
}

public readonly struct ReceiverTransferManager
{
    private readonly IBlockSequentialWriter _blockWriter;
    private readonly IByteReceiverAsync _byteReceiverAsync;

    public ReceiverTransferManager(IBlockSequentialWriter blockWriter, IByteReceiverAsync byteReceiverAsync)
    {
        _blockWriter = blockWriter;
        _byteReceiverAsync = byteReceiverAsync;
    }

    public async Task ReceiveBytesAsync(CancellationToken cancellationToken = default, CancellationToken pauseToken = default)
    {
        for (;;)
        {
            pauseToken.ThrowIfCancellationRequested();

            var message = await _byteReceiverAsync.ReceiveAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            switch (message.Type)
            {
                case MessageType.Data:
                    _blockWriter.WriteNextBlock(message.Data.Span);
                    break;
                case MessageType.EndOfTransfer:
                    return;
                case MessageType.Metadata:
                    goto default;
                default:
                    throw new IOException("Received unexpected message type.");
            }
        }
    }
}

public readonly struct ByteTransferManagerInterfaceWrapper
{
    private readonly IByteTransferManagerAsync _byteTransferManagerAsync;

    public ByteTransferManagerInterfaceWrapper(IByteTransferManagerAsync byteTransferManagerAsync)
    {
        _byteTransferManagerAsync = byteTransferManagerAsync;
    }

    public async Task SendAsync(MessageType type, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) =>
        await _byteTransferManagerAsync.SendAsync(type, data, cancellationToken);

    public async Task<ReceiveResult<ReadOnlyMemory<byte>>> ReceiveBytesAsync(CancellationToken cancellationToken = default) =>
        await _byteTransferManagerAsync.ReceiveAsync(cancellationToken);

    public async Task SendAsync(MessageType type, string data, CancellationToken cancellationToken = default) =>
        await _byteTransferManagerAsync.SendAsync(
            type,
            data,
            x => new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(x)),
            cancellationToken);

    public async Task<ReceiveResult<string>> ReceiveStringAsync(CancellationToken cancellationToken = default) =>
        await _byteTransferManagerAsync.ReceiveAsync(
            x => Encoding.UTF8.GetString(x.Span),
            cancellationToken);

    public async Task SendAsync(MessageType type, int data, CancellationToken cancellationToken = default) =>
        await _byteTransferManagerAsync.SendAsync(
            type,
            data,
            x => new ReadOnlyMemory<byte>(BitConverter.GetBytes(x)),
            cancellationToken);

    public async Task<ReceiveResult<int>> ReceiveLongAsync(CancellationToken cancellationToken = default) =>
        await _byteTransferManagerAsync.ReceiveAsync (
            bytes => BitConverter.ToInt32(bytes.Span),
            cancellationToken );

    public async Task SendEmptyMessageAsync(MessageType type, CancellationToken cancellationToken = default) =>
        await _byteTransferManagerAsync.SendAsync(type, cancellationToken: cancellationToken);

    public async Task<MessageType> ReceiveEmptyMessageAsync(CancellationToken cancellationToken = default)
    {
        var message = await _byteTransferManagerAsync.ReceiveAsync(cancellationToken);
        return message.Type;
    }

}

public readonly struct FileMetadata
{
    public void Deconstruct(out string filename, out ReadOnlyMemory<byte> fileHash, out int fileSize)
    {
        filename = Name;
        fileHash = Hash;
        fileSize = Size;
    }

    public string Name { get; init; }
    public ReadOnlyMemory<byte> Hash { get; init; }
    public int Size { get; init; }
}

public readonly struct ReceiverParameterCommunicationManager
{
    private readonly ByteTransferManagerInterfaceWrapper _byteTransferManager;

    public ReceiverParameterCommunicationManager(IByteTransferManagerAsync byteTransferManagerAsync)
    {
        _byteTransferManager = new ByteTransferManagerInterfaceWrapper(byteTransferManagerAsync);
    }

    public async Task ReceivePassword(string actualPassword,
        CancellationToken cancellationToken = default)
    {
        var passwordMessage = await _byteTransferManager.ReceiveStringAsync(cancellationToken);
        if (passwordMessage.Type != MessageType.Metadata)
            throw new IOException("Received unexpected message type.");

        if (actualPassword != string.Empty && actualPassword != passwordMessage.Data)
        {
            await _byteTransferManager.SendEmptyMessageAsync(MessageType.EndOfTransfer, cancellationToken);
            throw new InvalidPasswordException("Received password was incorrect.");
        }

        // Blank message to indicate password was correct/no password is required
        await _byteTransferManager.SendEmptyMessageAsync(MessageType.Metadata, cancellationToken);
    }

    public async Task<FileMetadata> ReceiveMetadataAsync(
        CancellationToken cancellationToken = default)
    {
        var fileNameMessage = await _byteTransferManager.ReceiveStringAsync(cancellationToken);

        switch (fileNameMessage)
        {
            case { Type: MessageType.Metadata }:
                break;
            case { Type: MessageType.EndOfTransfer }:
                throw new RemoteTransferCancelledException("Transfer was cancelled by the sender.");
            case { Type: MessageType.Data }:
                goto default;
            default:
                throw new IOException("Received unexpected message type.");
        }

        var fileHashMessage = await _byteTransferManager.ReceiveBytesAsync(cancellationToken);
        if (fileHashMessage.Type != MessageType.Metadata)
            throw new IOException("Received unexpected message type.");

        var fileSizeMessage = await _byteTransferManager.ReceiveLongAsync(cancellationToken);
        if (fileSizeMessage.Type != MessageType.Metadata)
            throw new IOException("Received unexpected message type.");

        return new FileMetadata{
            Name = fileNameMessage.Data,
            Hash = fileHashMessage.Data,
            Size = fileSizeMessage.Data
        };
    }

    public async Task SendLastWrittenBlockAsync(int block, CancellationToken cancellationToken = default) =>
        await _byteTransferManager.SendAsync(MessageType.Metadata, block, cancellationToken);
}

public readonly struct SenderParameterCommunicationManager
{
    private readonly ByteTransferManagerInterfaceWrapper _byteTransferManager;

    public SenderParameterCommunicationManager(IByteTransferManagerAsync byteTransferManagerAsync)
    {
        _byteTransferManager = new ByteTransferManagerInterfaceWrapper(byteTransferManagerAsync);
    }

    public async Task SendPassword(string password, CancellationToken cancellationToken = default)
    {
        await _byteTransferManager.SendAsync(MessageType.Metadata, password, cancellationToken);

        MessageType resultType = await _byteTransferManager.ReceiveEmptyMessageAsync(cancellationToken);
        if (resultType == MessageType.EndOfTransfer)
        {
            throw new InvalidPasswordException("The password was incorrect.");
        }
    }

    public async Task SendMetadataAsync(FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        var (filename, fileHash, fileSize) = metadata;

        await _byteTransferManager.SendAsync(MessageType.Metadata, filename, cancellationToken);
        await _byteTransferManager.SendAsync(MessageType.Metadata, fileHash, cancellationToken);
        await _byteTransferManager.SendAsync(MessageType.Metadata, fileSize, cancellationToken);
    }

    public async Task<int> ReceiveLastWrittenBlock(CancellationToken cancellationToken = default)
    {
        var message = await _byteTransferManager.ReceiveLongAsync(cancellationToken);

        switch (message)
        {
            case { Type: MessageType.Metadata }:
                return message.Data;
            case { Type: MessageType.EndOfTransfer }:
                throw new RemoteTransferCancelledException("Transfer was cancelled by the receiver.");
            case { Type: MessageType.Data }:
                goto default;
            default:
                throw new IOException("Received unexpected message type.");
        }
    }
}

public class TransferCancelledException : Exception
{
    public TransferCancelledException(string message) : base(message) { }
}

public class RemoteTransferCancelledException : Exception
{
    public RemoteTransferCancelledException(string message) : base(message) { }
}

public class LocalTransferCancelledException : Exception
{
    public LocalTransferCancelledException(string message) : base(message) { }
}

public class InvalidPasswordException : Exception
{
    public InvalidPasswordException(string message) : base(message) { }
}