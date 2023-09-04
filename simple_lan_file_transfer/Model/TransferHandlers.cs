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

    public async ValueTask SendBytesAsync(CancellationToken cancellationToken = default)
    {
        for (;;)
        {
            var block = _blockReader.ReadNextBlock();
            cancellationToken.ThrowIfCancellationRequested();

            if (block.LongLength < Utility.BlockSize)
            {
                await _byteSenderAsync.SendAsync(new ByteMessage<byte[]> { Type = ByteMessageType.EndOfTransfer }, cancellationToken);
                return;
            }
            
            await _byteSenderAsync.SendAsync(new ByteMessage<byte[]> { Data = block, Type = ByteMessageType.Data }, cancellationToken);
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
    
    public async ValueTask ReceiveBytesAsync(CancellationToken cancellationToken = default)
    {
        for (;;)
        {
            var message = await _byteReceiverAsync.ReceiveAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            switch (message.Type)
            {
                case ByteMessageType.Data:
                    _blockWriter.WriteNextBlock(message.Data);
                    break;
                case ByteMessageType.EndOfTransfer:
                    return;
                case ByteMessageType.Metadata:
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
    
    public ValueTask SendAsync(ByteMessage<byte[]> message, CancellationToken cancellationToken = default) =>
        _byteTransferManagerAsync.SendAsync(message, cancellationToken);

    public ValueTask<ByteMessage<byte[]>> ReceiveBytesAsync(CancellationToken cancellationToken = default) =>
        _byteTransferManagerAsync.ReceiveAsync(cancellationToken);
    
    public ValueTask SendAsync(ByteMessage<string> message, CancellationToken cancellationToken = default) =>
        _byteTransferManagerAsync.SendAsync(message, Encoding.UTF8.GetBytes, cancellationToken);

    public ValueTask<ByteMessage<string>> ReceiveStringAsync(CancellationToken cancellationToken = default) =>
        _byteTransferManagerAsync.ReceiveAsync(Encoding.UTF8.GetString, cancellationToken);
    
    public ValueTask SendAsync(ByteMessage<long> message, CancellationToken cancellationToken = default) =>
        _byteTransferManagerAsync.SendAsync(message, BitConverter.GetBytes, cancellationToken);
    
    public ValueTask<ByteMessage<long>> ReceiveLongAsync(CancellationToken cancellationToken = default) =>
        _byteTransferManagerAsync.ReceiveAsync(bytes => BitConverter.ToInt64(bytes), cancellationToken);
}

public readonly struct ReceiverParameterCommunicationManager
{
    private readonly ByteTransferManagerInterfaceWrapper _byteTransferManager;
    
    public ReceiverParameterCommunicationManager(IByteTransferManagerAsync byteTransferManagerAsync)
    {
        _byteTransferManager = new ByteTransferManagerInterfaceWrapper(byteTransferManagerAsync);
    } 
    
    public async ValueTask<(string filename, byte[] fileHash)> ReceiveMetadataAsync(CancellationToken cancellationToken = default)
    {
        var fileNameMessage = await _byteTransferManager.ReceiveStringAsync(cancellationToken);
        if (fileNameMessage.Type != ByteMessageType.Metadata)
            throw new IOException("Received unexpected message type.");
        
        var fileHashMessage = await _byteTransferManager.ReceiveBytesAsync(cancellationToken);
        if (fileHashMessage.Type != ByteMessageType.Metadata)
            throw new IOException("Received unexpected message type.");
        
        return (fileNameMessage.Data, fileHashMessage.Data);
    }
    
    public async ValueTask SendLastWrittenBlockAsync(long block, CancellationToken cancellationToken = default)
    {
        await _byteTransferManager.SendAsync(new ByteMessage<long>
        {
            Data = block,
            Type = ByteMessageType.Metadata
        }, cancellationToken);
    }
}

public readonly struct SenderParameterCommunicationManager
{
    private readonly ByteTransferManagerInterfaceWrapper _byteTransferManager;
    
    public SenderParameterCommunicationManager(IByteTransferManagerAsync byteTransferManagerAsync)
    {
        _byteTransferManager = new ByteTransferManagerInterfaceWrapper(byteTransferManagerAsync);
    }
    
    public async ValueTask SendMetadataAsync(string filename, byte[] fileHash, CancellationToken cancellationToken = default)
    {
        await _byteTransferManager.SendAsync(new ByteMessage<string>
        {
            Data = filename,
            Type = ByteMessageType.Metadata
        }, cancellationToken);
        
        await _byteTransferManager.SendAsync(new ByteMessage<byte[]>
        {
            Data = fileHash,
            Type = ByteMessageType.Metadata
        }, cancellationToken);
    }
    
    public async ValueTask<long> ReceiveLastWrittenBlock(CancellationToken cancellationToken = default)
    {
        var message = await _byteTransferManager.ReceiveLongAsync(cancellationToken);
        
        if (message.Type != ByteMessageType.Metadata)
            throw new IOException("Received unexpected message type.");

        return message.Data;
    }
}