namespace simple_lan_file_transfer.Models;

public class TransmitterTransferManager
{
    private readonly IBlockSequentialReader _blockReader;
    private readonly IByteSender _byteSender;
    
    public TransmitterTransferManager(IBlockSequentialReader blockReader, IByteSender byteSender)
    {
        _blockReader = blockReader;
        _byteSender = byteSender;
    }

    public async ValueTask SendBytesAsync(CancellationToken cancellationToken = default)
    {
        for (;;)
        {
            var block = _blockReader.ReadNextBlock();
            cancellationToken.ThrowIfCancellationRequested();

            if (block.LongLength < Utility.BlockSize)
            {
                await _byteSender.SendAsync(new ByteMessage<byte[]> { Type = ByteMessageType.EndOfTransfer }, cancellationToken);
                return;
            }
            
            await _byteSender.SendAsync(new ByteMessage<byte[]> { Data = block, Type = ByteMessageType.Data }, cancellationToken);
        }
    }
}

public class ReceiverTransferManager
{
    private readonly IBlockSequentialWriter _blockWriter;
    private readonly IByteReceiver _byteReceiver;
    
    public ReceiverTransferManager(IBlockSequentialWriter blockWriter, IByteReceiver byteReceiver)
    {
        _blockWriter = blockWriter;
        _byteReceiver = byteReceiver;
    }
    
    public async ValueTask ReceiveBytesAsync(CancellationToken cancellationToken = default)
    {
        for (;;)
        {
            var message = await _byteReceiver.ReceiveAsync(cancellationToken);
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
    
}

public class SenderParameterCommunicationManager
{
    
}