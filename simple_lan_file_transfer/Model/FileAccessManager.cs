using System.Text;
using System.Runtime.CompilerServices;

namespace simple_lan_file_transfer.Models;


/// <summary>
/// Interface for reading blocks of data sequentially
/// </summary>
public interface IBlockSequentialReader
{
    /// <summary>
    /// Reads the next block of data
    /// </summary>
    /// <returns>Block of data that was read</returns>
    ReadOnlyMemory<byte> ReadNextBlock();
}

/// <summary>
/// Interface for writing blocks of data sequentially
/// </summary>
public interface IBlockSequentialWriter
{
    /// <summary>
    /// Writes the next block of data
    /// </summary>
    /// <param name="data">Block to write</param>
    void WriteNextBlock(ReadOnlySpan<byte> data);
}

/// <summary>
/// Manages access to a file by reading and writing blocks of data sequentially. Also provides a notification mechanism
/// for the last processed block.
/// </summary>
public sealed class FileBlockAccessManager : IBlockSequentialReader, IBlockSequentialWriter, INotifyPropertyChanged, IDisposable
{
    private readonly byte[] _blockBuffer = new byte[Utility.BlockSize];

    private bool _disposed;
    private readonly Stream _fileStream;
    private readonly MetadataWriter? _metadataWriter;

    /// <summary>
    /// Event that is raised when the <see cref="LastProcessedBlock"/> property changes
    /// </summary>
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
    /// <summary>
    /// Last block that was processed (read or written) by this instance
    /// </summary>
    public int LastProcessedBlock
    {
        get => _lastProcessedBlock;
        private set => SetProperty(ref _lastProcessedBlock, value);
    }

    /// <summary>
    /// Size of the underlying file in bytes
    /// </summary>
    public int FileSize { get; init; }

    /// <summary>
    /// Creates a new instance of <see cref="FileBlockAccessManager"/> with the specified file stream and size.
    /// Also accepts an optional <see cref="MetadataWriter"/> that will be used to store metadata about the file.
    /// </summary>
    /// <param name="fileStream">Underlying file stream</param>
    /// <param name="fileSize">Size of the underlying stream</param>
    /// <param name="metadataWriter">If supplied, allows writing metadata when using this instance</param>
    public FileBlockAccessManager(Stream fileStream, int fileSize, MetadataWriter? metadataWriter = default)
    {
        _fileStream = fileStream;
        FileSize = fileSize;

        _metadataWriter = metadataWriter;
    }

    /// <summary>
    /// Seeks to the start of the specified block, if the underlying stream supports seeking.
    /// </summary>
    /// <param name="block">Index of the block to seek to</param>
    /// <returns>Bool value indicating whether the end of the stream was reached</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed</exception>
    public bool SeekToBlock(int block)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));
        if (!_fileStream.CanSeek) return false;

        _fileStream.Seek(block * Utility.BlockSize, SeekOrigin.Begin);
        LastProcessedBlock = block;

        return _fileStream.Position == _fileStream.Length;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed</exception>
    public ReadOnlyMemory<byte> ReadNextBlock()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));

        var read = _fileStream.Read(_blockBuffer);
        var block = new ReadOnlyMemory<byte>(_blockBuffer, 0, read);

        IncrementBlockCounter();

        return block;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed</exception>
    public void WriteNextBlock(ReadOnlySpan<byte> block)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));

        _fileStream.Write(block);

        SaveAndIncrementBlockCounter();
    }

    /// <summary>
    /// Dispose this instance, closing the underlying file stream and metadata writer if present
    /// </summary>
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

/// <summary>
/// Class used for reading metadata from a file
/// </summary>
public sealed class MetadataReader : IDisposable
{
    private bool _disposed;
    private readonly Stream _metadataFileStream;

    /// <summary>
    /// Creates a new instance of <see cref="MetadataReader"/> with the specified file stream. The stream is expected
    /// to be opened.
    /// </summary>
    /// <param name="metadataFile">The stream to read metadata from</param>
    public MetadataReader(Stream metadataFile)
    {
        _metadataFileStream = metadataFile;
    }

    /// <summary>
    /// Reads the metadata that indicates the last block that was written to the file this metadata belongs to.
    /// </summary>
    /// <returns>Last block that was read</returns>
    /// <exception cref="ObjectDisposedException">Thrown if this instance was disposed</exception>
    /// <exception cref="IOException">Thrown when the necessary amount of bytes wasn't read</exception>
    public int ReadFileLastWrittenBlock()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataWriter));

        var block = new byte[sizeof(int)];

        _metadataFileStream.Seek(0, SeekOrigin.Begin);
        var read = _metadataFileStream.Read(block);

        if (read != sizeof(int)) throw new IOException("Read less bytes than expected");

        return BitConverter.ToInt32(block);
    }

    /// <summary>
    /// Reads the file name saved inside the metadata file.
    /// </summary>
    /// <returns>Name of the file this metadata corresponds to</returns>
    /// <exception cref="ObjectDisposedException">Thrown if this instance was disposed</exception>
    /// <exception cref="IOException">Thrown when the necessary amount of bytes wasn't read</exception>
    public string ReadFileName()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataWriter));

        var fileName = new byte[_metadataFileStream.Length - sizeof(int)];

        _metadataFileStream.Seek(sizeof(int), SeekOrigin.Begin);
        var read = _metadataFileStream.Read(fileName);

        if (read != fileName.Length) throw new IOException("Read less bytes than expected");

        return Encoding.UTF8.GetString(fileName);
    }

    /// <summary>
    /// Disposes this instance and the underlying stream
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _metadataFileStream.Dispose();

        _disposed = true;
    }

}

/// <summary>
/// Class used for writing metadata to a file
/// </summary>
public sealed class MetadataWriter : IDisposable
{
    private bool _disposed;
    private readonly Stream _metadataFileStream;

    /// <summary>
    /// Creates a new instance of <see cref="MetadataWriter"/> with the specified file stream. The stream is expected
    /// to be opened and must support seeking. The stream will also have a 0 integer written to it.
    /// </summary>
    public MetadataWriter(Stream metadataFile)
    {
        _metadataFileStream = metadataFile;

        WriteLastBlockProcessed(0);
        WriteFileName(string.Empty);
    }

    /// <summary>
    /// Writes the specified block index to the metadata file
    /// </summary>
    /// <param name="block">Number to write</param>
    /// <exception cref="ObjectDisposedException">Thrown if this instance was disposed</exception>
    public void WriteLastBlockProcessed(int block)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataWriter));

        _metadataFileStream.Seek(0, SeekOrigin.Begin);
        _metadataFileStream.Write(BitConverter.GetBytes(block));
        _metadataFileStream.Flush();
    }

    /// <summary>
    /// Writes the specified file name to the metadata file
    /// </summary>
    /// <param name="fileName">Filename to write</param>
    /// <exception cref="ObjectDisposedException">Thrown if this instance was disposed</exception>
    public void WriteFileName(string fileName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataWriter));

        // Truncate file to new size
        _metadataFileStream.SetLength(sizeof(int) + fileName.Length);

        _metadataFileStream.Seek(sizeof(int), SeekOrigin.Begin);
        _metadataFileStream.Write(Encoding.UTF8.GetBytes(fileName));
        _metadataFileStream.Flush();
    }

    /// <summary>
    /// Disposes this instance and the underlying stream
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _metadataFileStream.Dispose();

        _disposed = true;
    }
}