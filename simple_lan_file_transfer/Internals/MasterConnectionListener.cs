namespace simple_lan_file_transfer.Internals;

public class MasterConnectionListener : NetworkLoopBase
{
    private readonly TcpListener _listener = new(IPAddress.Any, Utility.DefaultPort);
    public List <SingleConnectionManager> Connections { get; } = new();


    protected override async void Loop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket socket = await _listener.AcceptSocketAsync(cancellationToken);
            Connections.Add(new SingleConnectionManager(socket));
        }
    }
}