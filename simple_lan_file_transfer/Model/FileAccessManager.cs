using System.Text;
using System.Runtime.CompilerServices;

namespace simple_lan_file_transfer.Models;


public interface IBlockSequentialReader 
{
    byte[] ReadNextBlock();
}

public interface IBlockSequentialWriter
{
    void WriteNextBlock(byte[] data);
}

public sealed class FileBlockAccessManager : IBlockSequentialReader, IBlockSequentialWriter, INotifyPropertyChanged, IDisposable
{
    private bool _disposed;
    private readonly FileStream _fileStream;
    private readonly MetadataHandler _metadataHandler;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private long _lastProcessedBlock;
    public long LastProcessedBlock
    {
        get => _lastProcessedBlock;
        private set => SetProperty(ref _lastProcessedBlock, value);
    }
    
    public long FileBlocksCount { get; }

    public FileBlockAccessManager(FileStream fileStream, MetadataHandler metadataHandler)
    {
        _fileStream = fileStream;
        FileBlocksCount = CalculateFileBlockCount(_fileStream.Length);
        
        _metadataHandler = metadataHandler;
    }
    
    public bool SeekToBlock(long block)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));
    
        _fileStream.Seek(block * Utility.BlockSize, SeekOrigin.Begin);
        
        return _fileStream.Position == _fileStream.Length;
    }
    
    public byte[] ReadNextBlock()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));
        
        var block = new byte[Utility.BlockSize];
        IncrementBlockCounter();

        return block;
    }
    
    public void WriteNextBlock(byte[] block)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileBlockAccessManager));
        
        _fileStream.Write(block);
        SaveAndIncrementBlockCounter();
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _fileStream.Dispose();
        _metadataHandler.Dispose();
        
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
        
        _metadataHandler.WriteLastBlockProcessed(LastProcessedBlock);
        IncrementBlockCounter();
    }

    private static long CalculateFileBlockCount(long fileSize)
    {
        return (long) Math.Ceiling(fileSize / (double) Utility.BlockSize);
    }
}

public sealed class MetadataHandler : IDisposable
{
    public static class MetadataHandlerFactory
    {
        public static MetadataHandler CreateMetadataHandler(string metadataFilePath)
        {
            FileStream metadataFileStream = OpenMetadataFile(metadataFilePath);
            MetadataHandler metadataHandler = new(metadataFileStream);
            return metadataHandler;
        }
        private static FileStream OpenMetadataFile(string filePath)
        {
            return !File.Exists(filePath) ? CreateNewMetadataFile(filePath) : OpenExistingMetadataFile(filePath);
        }
        
        private static FileStream CreateNewMetadataFile(string metadataFilePath)
        {
            return File.Create(metadataFilePath);
        }
        
        private static FileStream OpenExistingMetadataFile(string metadataFilePath)
        {
            return File.Open(metadataFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
    }
    
    private bool _disposed;
    private readonly FileStream _metadataFileStream;

    private MetadataHandler(FileStream metadataFile)
    {
        _metadataFileStream = metadataFile;
        
        WriteLastBlockProcessed(0);
        WriteFileName(string.Empty);
    }
    
    public long ReadFileLastWrittenBlock()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataHandler));
        
        var block = new byte[sizeof(long)];
        
        _metadataFileStream.Seek(0, SeekOrigin.Begin);
        var read = _metadataFileStream.Read(block);

        if (read != sizeof(long)) throw new IOException("Read less bytes than expected");
        
        return BitConverter.ToInt64(block);
    }
    
    public string ReadFileName()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataHandler));
        
        var fileName = new byte[_metadataFileStream.Length - sizeof(long)];
        
        _metadataFileStream.Seek(sizeof(long), SeekOrigin.Begin);
        var read = _metadataFileStream.Read(fileName);

        if (read != fileName.Length) throw new IOException("Read less bytes than expected");
        
        return Encoding.UTF8.GetString(fileName);
    }
    
    public void WriteLastBlockProcessed(long block)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataHandler));
        
        _metadataFileStream.Seek(0, SeekOrigin.Begin);
        _metadataFileStream.Write(BitConverter.GetBytes(block));
        _metadataFileStream.Flush();
    }
    
    public void WriteFileName(string fileName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MetadataHandler));
        
        // Truncate file to new size
        _metadataFileStream.SetLength(sizeof(long) + fileName.Length);
        
        _metadataFileStream.Seek(sizeof(long), SeekOrigin.Begin);
        _metadataFileStream.Write(Encoding.UTF8.GetBytes(fileName));
        _metadataFileStream.Flush();
    }
    
    public void DeleteMetadataFile()
    {
        var metadataFilePath = _metadataFileStream.Name;
        
        _metadataFileStream.Close();
        File.Delete(metadataFilePath);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _metadataFileStream.Dispose();
        
        _disposed = true;
    }
}