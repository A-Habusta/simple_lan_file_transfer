namespace simple_lan_file_transfer.Models;

public sealed class LocalNetworkAvailabilityBroadcastHandler : IDisposable
{
    private class LocalNetworkAvailabilityBroadcastSender : NetworkLoopBase
    {
        private readonly List<(UdpClient, byte[])> _broadcastedAddressesPerInterface = new();
        public LocalNetworkAvailabilityBroadcastSender()
        {
            PopulateBroadcastedAddressesPerInterface();
        }

        protected override async Task LoopAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            for(;;)
            {
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

        private void PopulateBroadcastedAddressesPerInterface()
        {
            var addresses = FindAllLocalAddressInfo();
            foreach (UnicastIPAddressInformation addressInfo in addresses)
            {
                IPAddress broadcastAddress = CalculateNetworkBroadcastAddress(addressInfo);

                var bytes = broadcastAddress.GetAddressBytes();
                var client = new UdpClient(new IPEndPoint(addressInfo.Address, Utility.DefaultBroadcastPort));
                _broadcastedAddressesPerInterface.Add((client, bytes));
            }
        }

        private static List<UnicastIPAddressInformation> FindAllLocalAddressInfo()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            return interfaces
                .Where(@interface => @interface.OperationalStatus == OperationalStatus.Up)
                .Where(@interface => @interface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(@interface => @interface.GetIPProperties().UnicastAddresses)
                .Where(addressInfo => addressInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                .ToList();

        }

        private static IPAddress CalculateNetworkBroadcastAddress(UnicastIPAddressInformation addressInfo)
        {
            var bytes = addressInfo.Address.GetAddressBytes();
            var subnetMaskBytes = addressInfo.IPv4Mask.GetAddressBytes();

            var broadcastBytes = new byte[bytes.Length];

            // Sets all network bits to 1, which is the network broadcast address
            for (var i = 0; i < bytes.Length; i++)
            {
                broadcastBytes[i] = (byte) (bytes[i] | ~subnetMaskBytes[i]);
            }

            return new IPAddress(broadcastBytes);
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

    private class LocalNetworkAvailabilityBroadcastReceiver : NetworkLoopBase
    {
        private readonly UdpClient _broadcastListener = new(Utility.DefaultBroadcastPort);
        public ObservableCollection<IPAddress> AvailableIpAddresses { get; } = new();

        protected override async Task LoopAsync(CancellationToken cancellationToken)
        {
            AvailableIpAddresses.Clear();
            for(;;)
            {
                cancellationToken.ThrowIfCancellationRequested();

                UdpReceiveResult result = await _broadcastListener.ReceiveAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var ipAddress = new IPAddress(result.Buffer);
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