using simple_lan_file_transfer.Models;

namespace simple_lan_file_transfer.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MasterConnectionManager ConnectionManager { get; } = new(Utility.DefaultPort);
    
    private static string GetIpAddressFromTransfer()
    {
        throw new NotImplementedException();
    }
}
