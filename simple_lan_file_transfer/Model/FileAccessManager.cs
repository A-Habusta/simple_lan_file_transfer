using System.Text;
using System.Runtime.CompilerServices;

namespace simple_lan_file_transfer.Models;


public interface IBlockSequentialReader
{
    ReadOnlyMemory<byte> ReadNextBlock();
}

public interface IBlockSequentialWriter
{
    void WriteNextBlock(ReadOnlySpan<byte> data);
}

public sealed class FileBlockAccessManager : IBlockSequentialReader, IBlockSequentialWriter, INotifyPropertyChanged, IDisposable
{
    private readonly byte[] _blockBuffer = new byte[Utility.BlockSize];

    private bool _disposed;
    private readonly Stream _fileStream;
    private readonly MetadataWriter? _metadataWriter;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private int _lastProcessedBlock;
    public int LastProcessedBlock
    {
        get => _lastProcessedBlock;
        private set => SetProperty(ref _lastProcessedBlock, value);
    }

    public int FileSize { get; init; }

    public FileBlockAccessManager(Stream fileStream, int fileSize, MetadataWriter? metadataWriter = default)
    {
        _fileStream = fileStream;
        FileSize = fileSize;

        _metadataWriter = metadataWriter;
    }

    public bool SeekToBlock(int block)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));
        if (!_fileStream.CanSeek) return false;

        _fileStream.Seek(block * Utility.BlockSize, SeekOrigin.Begin);

        return _fileStream.Position == _fileStream.Length;
    }

    public ReadOnlyMemory<byte> ReadNextBlock()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));

        var read = _fileStream.Read(_blockBuffer);
        var block = new ReadOnlyMemory<byte>(_blockBuffer, 0, read);

        IncrementBlockCounter();

        return block;
    }

    public void WriteNextBlock(ReadOnlySpan<byte> block)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));

        _fileStream.Write(block);

        SaveAndIncrementBlockCounter();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _fileStream.Dispose();
        _metadataWriter?.Dispose();

        _disposed = true;
    }

    private void IncrementBlockCounter()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));

        ++LastProcessedBlock;
    }

    private void SaveAndIncrementBlockCounter()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));

        _metadataWriter?.WriteLastBlockProcessed(LastProcessedBlock);
        IncrementBlockCounter();
    }
}

public sealed class MetadataReader : IDisposable
{
    private bool _disposed;
    private readonly Stream _metadataFileStream;

    public MetadataReader(Stream metadataFile)
    {
        _metadataFileStream = metadataFile;
    }

    public int ReadFileLastWrittenBlock()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataWriter));

        var block = new byte[sizeof(int)];

        _metadataFileStream.Seek(0, SeekOrigin.Begin);
        var read = _metadataFileStream.Read(block);

        if (read != sizeof(int)) throw new IOException("Read less bytes than expected");

        return BitConverter.ToInt32(block);
    }

    public string ReadFileName()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataWriter));

        var fileName = new byte[_metadataFileStream.Length - sizeof(int)];

        _metadataFileStream.Seek(sizeof(int), SeekOrigin.Begin);
        var read = _metadataFileStream.Read(fileName);

        if (read != fileName.Length) throw new IOException("Read less bytes than expected");

        return Encoding.UTF8.GetString(fileName);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _metadataFileStream.Dispose();

        _disposed = true;
    }

}
public sealed class MetadataWriter : IDisposable
{
    private bool _disposed;
    private readonly Stream _metadataFileStream;

    public MetadataWriter(Stream metadataFile)
    {
        _metadataFileStream = metadataFile;

        WriteLastBlockProcessed(0);
        WriteFileName(string.Empty);
    }

    public void WriteLastBlockProcessed(int block)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataWriter));

        _metadataFileStream.Seek(0, SeekOrigin.Begin);
        _metadataFileStream.Write(BitConverter.GetBytes(block));
        _metadataFileStream.Flush();
    }

    public void WriteFileName(string fileName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataWriter));

        // Truncate file to new size
        _metadataFileStream.SetLength(sizeof(int) + fileName.Length);

        _metadataFileStream.Seek(sizeof(int), SeekOrigin.Begin);
        _metadataFileStream.Write(Encoding.UTF8.GetBytes(fileName));
        _metadataFileStream.Flush();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _metadataFileStream.Dispose();

        _disposed = true;
    }
}