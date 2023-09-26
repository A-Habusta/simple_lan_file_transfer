using System.Runtime.InteropServices;

namespace simple_lan_file_transfer.Models;

/// <summary>
/// This class is responsible for broadcasting and listening for local network availability.
/// </summary>
public sealed class LocalNetworkAvailabilityBroadcastHandler : IDisposable
{
    private class LocalNetworkAvailabilityBroadcastSender : NetworkLoopBase
    {
        private readonly List<(UdpClient, byte[])> _broadcastedAddressesPerInterface = new();
        public LocalNetworkAvailabilityBroadcastSender()
        {
            PopulateBroadcastedAddressesPerInterface();
        }

        /// <summary>
        /// This method is called in a loop in the base class. It sends a broadcast message to all available interfaces
        /// containing the local IP address of the corresponding interface.
        /// </summary>
        /// <param name="cancellationToken"/>
        protected override async Task LoopAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            for(;;)
            {
                tasks.Clear();
                cancellationToken.ThrowIfCancellationRequested();

                foreach ((UdpClient client, var bytes) in _broadcastedAddressesPerInterface)
                {
                    var task = Task.Run(async () => await client.SendAsync(bytes, cancellationToken), cancellationToken);
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(Utility.BroadcastIntervalMs, cancellationToken);
            }
        }


        /// <summary>
        /// Populates the instance's list of broadcasted addresses. Each address is associated with a <see cref="UdpClient"/>
        /// used for broadcasting to the interface corresponding to said address.
        /// </summary>
        private void PopulateBroadcastedAddressesPerInterface()
        {
            ClearBroadcastedAddressesPerInterface();

            var addresses = Utility.FindAllLocalAddressInfo();
            foreach (UnicastIPAddressInformation addressInfo in addresses)
            {
                IPAddress broadcastAddress = Utility.CalculateNetworkBroadcastAddress(addressInfo);

                var bytes = addressInfo.Address.GetAddressBytes();
                Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                var localEndPoint = new IPEndPoint(addressInfo.Address, 0);
                var remoteEndPoint = new IPEndPoint(broadcastAddress, Utility.DefaultBroadcastPort);

                // This should not be done on Windows, reason here: https://stackoverflow.com/a/14388707
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                }

                socket.Bind(localEndPoint);
                socket.EnableBroadcast = true;

                UdpClient client = new() { Client = socket };
                client.Connect(remoteEndPoint);

                _broadcastedAddressesPerInterface.Add((client, bytes));
            }
        }

        private void ClearBroadcastedAddressesPerInterface()
        {
            foreach ((UdpClient client, _) in _broadcastedAddressesPerInterface)
            {
                client.Dispose();
            }

            _broadcastedAddressesPerInterface.Clear();
        }


        protected override void Dispose(bool disposing)
        {
            if (Disposed) return;

            if (disposing)
            {
                _broadcastedAddressesPerInterface.ForEach(tuple => tuple.Item1.Dispose());
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Class used for listening to local network availability broadcasts. It listens for broadcast messages and adds
    /// any received IP addresses to its <see cref="AvailableIpAddresses"/> collection.
    /// </summary>
    private class LocalNetworkAvailabilityBroadcastReceiver : NetworkLoopBase
    {
        private readonly UdpClient _broadcastListener;
        public ObservableCollection<IPAddress> AvailableIpAddresses { get; } = new();

        /// <summary>
        /// Creates a new instance of <see cref="LocalNetworkAvailabilityBroadcastReceiver"/>. The instance will listen
        /// for incoming datagrams on the default broadcast port specified in the <see cref="Utility"/> class.
        /// </summary>
        public LocalNetworkAvailabilityBroadcastReceiver()
        {
            Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }

            var localEndPoint = new IPEndPoint(IPAddress.Any, Utility.DefaultBroadcastPort);

            socket.ReceiveTimeout = 0;
            socket.Bind(localEndPoint);

            _broadcastListener = new UdpClient { Client = socket };
        }

        /// <summary>
        /// Continuously listens for incoming datagrams
        /// </summary>
        protected override async Task LoopAsync(CancellationToken cancellationToken)
        {
            AvailableIpAddresses.Clear();
            for(;;)
            {
                cancellationToken.ThrowIfCancellationRequested();

                UdpReceiveResult result = await _broadcastListener.ReceiveAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var ipAddress = new IPAddress(result.Buffer);

                // We don't want to add our own IP address to the list
                if (IsIpAddressOurs(ipAddress)) continue;

                AvailableIpAddresses.Add(ipAddress);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (Disposed) return;

            if (disposing)
            {
                _broadcastListener.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Checks if the specified IP address is one from this machine
        /// </summary>
        /// <param name="ipAddress">IP address to check</param>
        /// <returns>Boolean indicating whether the specified address is ours</returns>
        private bool IsIpAddressOurs(IPAddress ipAddress)
        {
            var addresses = Utility.FindAllLocalAddressInfo();
            return addresses.Any(addressInfo => addressInfo.Address.Equals(ipAddress));
        }
    }

    private readonly LocalNetworkAvailabilityBroadcastSender _localNetworkAvailabilityBroadcastSender = new();
    private readonly LocalNetworkAvailabilityBroadcastReceiver _localNetworkAvailabilityBroadcastReceiver = new();

    public ObservableCollection<IPAddress> AvailableIpAddresses => _localNetworkAvailabilityBroadcastReceiver.AvailableIpAddresses;

    public void StartBroadcast() => _localNetworkAvailabilityBroadcastSender.RunLoop();
    public void StopBroadcast() => _localNetworkAvailabilityBroadcastSender.StopLoop();

    public void StartListening() => _localNetworkAvailabilityBroadcastReceiver.RunLoop();
    public void StopListening() => _localNetworkAvailabilityBroadcastReceiver.StopLoop();

    public void Dispose()
    {
        _localNetworkAvailabilityBroadcastSender.Dispose();
        _localNetworkAvailabilityBroadcastReceiver.Dispose();
    }
}