namespace simple_lan_file_transfer.Internals;

public class MasterConnectionManager
{
    public List<SingleConnectionManager> Connections => _listener.Connections;
    public List<IPAddress> AvailableIps => _ipBroadcastHandler.AvailableIpAddresses;

    private readonly LocalNetworkAvailabilityBroadcastHandler _ipBroadcastHandler = new();
    private readonly MasterConnectionListener _listener = new();

    public void Stop()
    {
        _listener.Stop();
        _ipBroadcastHandler.StopBroadcast();
        _ipBroadcastHandler.StopListening();
    }

    public async void ConnectTo(IPAddress ipAddress)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(ipAddress, Utility.DefaultPort);
        Connections.Add(new SingleConnectionManager(socket));
    }
}