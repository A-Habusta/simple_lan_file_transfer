using System.Security.Cryptography;

namespace simple_lan_file_transfer.Internals;

public abstract class FileAccessManager
{
    private readonly string _receiveRootDirectory;
    private FileStream? metadataFileStream;
    private string _fileName;

    private HashAlgorithm _hashAlgorithm;
    
    protected FileAccessManager(string receiveRootDirectory, string fileName)
    {
        _hashAlgorithm = MD5.Create();
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

    public int GetFileLastReadBlock(string metadataFilename)
    {
        var metadataPath = Path.Combine(_receiveRootDirectory, Utility.DefaultMetadataDirectory);
        throw new NotImplementedException();
    }
}