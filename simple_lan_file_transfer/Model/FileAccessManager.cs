using System.Security.Cryptography;
using System.Text;

namespace simple_lan_file_transfer.Internals;

// These classes won't use async methods because of significant performance loss when using them for small reads/writes. 

public abstract class FileAccessManager : IDisposable
{
    protected bool Disposed;
    
    protected FileStream? FileStream;
    private HashAlgorithm _hashAlgorithm;

    protected FileAccessManager()
    {
        _hashAlgorithm = MD5.Create();
    }
    
    public FileAccessManager(FileStream fileStream) : this()
    {
        FileStream = fileStream;
    }
    
    public void SeekToBlock(long block)
    {
        if (FileStream is null) return;
        
        FileStream!.Seek(block * Utility.BlockSize, SeekOrigin.Begin);
    }
    
    public async Task<byte[]> GetFileHashAsync(CancellationToken cancellationToken = default)
    {
        if (FileStream is null) return Array.Empty<byte>();
        
        FileStream!.Seek(0, SeekOrigin.Begin);
        return await _hashAlgorithm.ComputeHashAsync(FileStream!, cancellationToken);
    }

    public void Dispose()
    {
        if (Disposed) return;
        
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (Disposed) return;
        
        if (disposing)
        {
            FileStream?.Dispose();
        }
        
        Disposed = true;
    }
}

//TODO Implement opening file for writing
public sealed class WriterFileAccessManager : FileAccessManager
{
    private readonly string _receiveRootDirectory;
    private FileStream? _metadataFileStream;
    private string _fileName;

    private long _blockCounter;

    public WriterFileAccessManager(string receiveRootDirectory, string fileName)
    {
        _receiveRootDirectory = receiveRootDirectory;
        _fileName = fileName;
        
        if (!Directory.Exists(_receiveRootDirectory))
        {
            Directory.CreateDirectory(_receiveRootDirectory);
        }
        
        var metadataDirectory = Path.Combine(_receiveRootDirectory, Utility.DefaultMetadataDirectory);
        if (!Directory.Exists(metadataDirectory))
        {
            Directory.CreateDirectory(metadataDirectory);
        }
    }
    
    public void WriteNextBlock(byte[] block)
    {
        if (FileStream is null) return;
        
        FileStream!.Write(block);
        
        WriteLastBlockWritten(_blockCounter++);
    }

    public void OpenMetadataFile(byte[] fileHash)
    {
        var fileName = BitConverter.ToString(fileHash);
        
        var metadataPath = Path.Combine(_receiveRootDirectory, Utility.DefaultMetadataDirectory);
        var metadataFilePath = Path.Combine(metadataPath, fileName);


        if (!File.Exists(metadataFilePath))
        {
            CreateNewMetadataFile(metadataFilePath);
        }
        else
        {
            OpenExistingMetadataFile(metadataFilePath);
        }
    }
    
    public long ReadFileLastWrittenBlock()
    {
        if (_metadataFileStream is null) return 0;
        
        var block = new byte[sizeof(long)];
        
        _metadataFileStream.Seek(0, SeekOrigin.Begin);
        var read = _metadataFileStream.Read(block);

        if (read != sizeof(long)) throw new IOException("Read less bytes than expected");
        
        return BitConverter.ToInt64(block);
    }
    
    
    private string ReadFileName()
    {
        if (_metadataFileStream is null) return string.Empty;
        
        var fileName = new byte[_metadataFileStream.Length - sizeof(long)];
        
        _metadataFileStream.Seek(sizeof(long), SeekOrigin.Begin);
        var read = _metadataFileStream.Read(fileName);

        if (read != fileName.Length) throw new IOException("Read less bytes than expected");
        
        return Encoding.UTF8.GetString(fileName);
    }
    
    private void WriteLastBlockWritten(long block)
    {
        _metadataFileStream!.Seek(sizeof(long), SeekOrigin.Begin);
        _metadataFileStream!.Write(BitConverter.GetBytes(block));
        _metadataFileStream!.Flush();
    }
    
    private void WriteFileName(string fileName)
    {
        _metadataFileStream!.Seek(sizeof(long), SeekOrigin.Begin);
        _metadataFileStream!.Write(Encoding.UTF8.GetBytes(fileName));
        _metadataFileStream!.Flush();
    }
    
    private void CreateNewMetadataFile(string metadataFilePath)
    {
        _metadataFileStream = File.Create(metadataFilePath);
        
        WriteLastBlockWritten(_blockCounter);
        WriteFileName(_fileName);
        
        _metadataFileStream.Flush();
    }
    
    private void OpenExistingMetadataFile(string metadataFilePath)
    {
        _metadataFileStream = File.Open(metadataFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
    
    private void DeleteMetadataFile()
    {
        if (_metadataFileStream is null) return;
        
        var metadataFilePath = _metadataFileStream.Name;
        
        _metadataFileStream?.Close();
        File.Delete(metadataFilePath);
    }
    
    protected override void Dispose(bool disposing)
    {
        if (Disposed) return;
        
        if (disposing)
        {
            _metadataFileStream?.Dispose();
        }
        
        base.Dispose(disposing);
    }
}

public sealed class ReaderFileAccessManager : FileAccessManager
{
    public ReaderFileAccessManager(FileStream fileStream) : base(fileStream) {}
    
    public byte[] ReadNextBlock()
    {
        if (FileStream is null) return Array.Empty<byte>();
        
        const int blockSize = Utility.BlockSize;
        
        var block = new byte[blockSize];
        var read = FileStream!.Read(block);
        
        if (read != blockSize) Array.Resize(ref block, read);
        
        return block;
    }
}

