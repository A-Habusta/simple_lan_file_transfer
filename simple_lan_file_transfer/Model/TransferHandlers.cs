namespace simple_lan_file_transfer.Models;

public class TransmitterTransferManager
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

public class ReceiverTransferManager
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

public class ReceiverParameterCommunicationManager
{
    private readonly IByteTransferManagerAsync _byteTransferManagerAsync;

    public ReceiverParameterCommunicationManager(IByteTransferManagerAsync byteTransferManagerAsync)
    {
        _byteTransferManagerAsync = byteTransferManagerAsync;
    } 
    
    private async ValueTask
}

public class SenderParameterCommunicationManager
{
    private readonly IByteTransferManagerAsync _byteTransferManagerAsync;
    
    public SenderParameterCommunicationManager(IByteTransferManagerAsync byteTransferManagerAsync)
    {
        _byteTransferManagerAsync = byteTransferManagerAsync;
    }
}