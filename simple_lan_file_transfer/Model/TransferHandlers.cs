using System.Text;

namespace simple_lan_file_transfer.Models;

/// <summary>
/// Wraps the <see cref="IBlockSequentialReader"/> and <see cref="IByteSenderAsync"/> interfaces and provides a simple
/// method to send data from the <see cref="IBlockSequentialReader"/> to the <see cref="IByteSenderAsync"/>.
/// </summary>
public readonly struct TransmitterTransferManager
{
    private readonly IBlockSequentialReader _blockReader;
    private readonly IByteSenderAsync _byteSenderAsync;

    public TransmitterTransferManager(IBlockSequentialReader blockReader, IByteSenderAsync byteSenderAsync)
    {
        _blockReader = blockReader;
        _byteSenderAsync = byteSenderAsync;
    }

    /// <summary>
    /// Sends the data from the <see cref="IBlockSequentialReader"/> to the <see cref="IByteSenderAsync"/>, until there
    /// is data in the <see cref="IBlockSequentialReader"/>. Sends an empty message with the type EndOfTransfer when
    /// the transfer is finished.
    /// </summary>
    /// <param name="cancellationToken">Cancels the task wherever</param>
    /// <param name="pauseToken">Cancels the transfer only when a loop iteration is finished</param>
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

/// <summary>
/// Wraps the <see cref="IBlockSequentialWriter"/> and <see cref="IByteReceiverAsync"/> interfaces and provides a
/// simple way to receive data from the <see cref="IByteReceiverAsync"/> and write it to the
/// <see cref="IBlockSequentialWriter"/>.
/// </summary>
public readonly struct ReceiverTransferManager
{
    private readonly IBlockSequentialWriter _blockWriter;
    private readonly IByteReceiverAsync _byteReceiverAsync;

    public ReceiverTransferManager(IBlockSequentialWriter blockWriter, IByteReceiverAsync byteReceiverAsync)
    {
        _blockWriter = blockWriter;
        _byteReceiverAsync = byteReceiverAsync;
    }

    /// <summary>
    /// Receives data from the <see cref="IByteReceiverAsync"/> and writes it to the <see cref="IBlockSequentialWriter"/>.
    /// Receives an empty message with the type EndOfTransfer when the transfer is finished.
    /// </summary>
    /// <param name="cancellationToken">Cancels the task wherever</param>
    /// <param name="pauseToken">Cancels the transfer only when a loop iteration is finished</param>
    /// <exception cref="IOException">Thrown when an unknown message type is received</exception>
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

/// <summary>
/// Wraps the <see cref="IByteTransferManagerAsync"/> interface and provides pre-made method for sending and receiving
/// various data types.
/// </summary>
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

/// <summary>
/// Structure used for storing the metadata of a file for easier handling.
/// </summary>
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

/// <summary>
/// Manages the communication between the sender and the receiver of the file transfer on the sender's side.
/// </summary>
public readonly struct ReceiverParameterCommunicationManager
{
    private readonly ByteTransferManagerInterfaceWrapper _byteTransferManager;

    public ReceiverParameterCommunicationManager(IByteTransferManagerAsync byteTransferManagerAsync)
    {
        _byteTransferManager = new ByteTransferManagerInterfaceWrapper(byteTransferManagerAsync);
    }

    /// <summary>
    /// Receives a password from the sender and compares it to the expected password. If the passwords don't match,
    /// throws an <see cref="InvalidPasswordException"/> and sends an empty message with the type EndOfTransfer.
    /// If the local password is empty, sends an empty message with the type Metadata to indicate that no password is
    /// expected. This message is also sent when the passwords match.
    /// </summary>
    /// <param name="actualPassword">Expected password</param>
    /// <param name="cancellationToken"/>
    /// <exception cref="IOException">Throw when an unexpected message type is received</exception>
    /// <exception cref="InvalidPasswordException">Throw when the received password is invalid</exception>
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

    /// <summary>
    /// Communicates with the sender to receive the metadata of the file being transferred.
    /// </summary>
    /// <param name="cancellationToken"/>
    /// <returns>Received file metadata</returns>
    /// <exception cref="RemoteTransferCancelledException">Thrown when an EndOfTransfer message is received</exception>
    /// <exception cref="IOException">Thrown when an unexpected message type is received</exception>
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

    /// <summary>
    /// Send the last written block to the sender to indicate where to start the transfer from.
    /// </summary>
    /// <param name="block">Last block that was written for the file to be received</param>
    /// <param name="cancellationToken"/>
    public async Task SendLastWrittenBlockAsync(int block, CancellationToken cancellationToken = default) =>
        await _byteTransferManager.SendAsync(MessageType.Metadata, block, cancellationToken);
}

/// <summary>
/// Responsible for managing the communication between the sender and the receiver of the file transfer on the sender's
/// side.
/// </summary>
public readonly struct SenderParameterCommunicationManager
{
    private readonly ByteTransferManagerInterfaceWrapper _byteTransferManager;

    public SenderParameterCommunicationManager(IByteTransferManagerAsync byteTransferManagerAsync)
    {
        _byteTransferManager = new ByteTransferManagerInterfaceWrapper(byteTransferManagerAsync);
    }

    /// <summary>
    /// Sends the specified password to the receiver. If the receiver responds with an EndOfTransfer message, it means
    /// that the password was invalid and we throw an <see cref="InvalidPasswordException"/>. If any other message
    /// type is received that means the password was the correct one (or that no password is expected) and we continue.
    /// </summary>
    /// <param name="password">Password to be sent</param>
    /// <param name="cancellationToken"/>
    /// <exception cref="InvalidPasswordException">Thrown when the sent password is not correct</exception>
    public async Task SendPassword(string password, CancellationToken cancellationToken = default)
    {
        await _byteTransferManager.SendAsync(MessageType.Metadata, password, cancellationToken);

        MessageType resultType = await _byteTransferManager.ReceiveEmptyMessageAsync(cancellationToken);
        if (resultType == MessageType.EndOfTransfer)
        {
            throw new InvalidPasswordException("The password was incorrect.");
        }
    }

    /// <summary>
    /// Send metadata of the file that will be transferred to the receiver.
    /// </summary>
    /// <param name="metadata">Struct containing the metadata to send</param>
    /// <param name="cancellationToken"/>
    public async Task SendMetadataAsync(FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        var (filename, fileHash, fileSize) = metadata;

        await _byteTransferManager.SendAsync(MessageType.Metadata, filename, cancellationToken);
        await _byteTransferManager.SendAsync(MessageType.Metadata, fileHash, cancellationToken);
        await _byteTransferManager.SendAsync(MessageType.Metadata, fileSize, cancellationToken);
    }

    /// <summary>
    /// Receives the last written block from the receiver to indicate where to start the transfer from.
    /// </summary>
    /// <param name="cancellationToken"/>
    /// <returns>Received file metadata</returns>
    /// <exception cref="RemoteTransferCancelledException">Thrown when an EndOfTransfer message is received</exception>
    /// <exception cref="IOException">Thrown when an unexpected message type is received</exception>
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