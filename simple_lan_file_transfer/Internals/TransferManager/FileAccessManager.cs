namespace simple_lan_file_transfer.Internals;

public class FileAccessManager
{
    private readonly string _rootDirectory;
    private string _fileName = string.Empty;
    
    public FileAccessManager(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }
}