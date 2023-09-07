namespace simple_lan_file_transfer.Models;

internal static class Utility
{
    public const int  BytesInKiloByte = 1024;

    public const int BlockSizeKb = 1024;
    public const int BlockSize = BlockSizeKb * BytesInKiloByte;

    public const int BufferSizeKb = 8;
    public const int BufferSize = BufferSizeKb * BytesInKiloByte;

    public const ushort DefaultPort = 52123;
    public const ushort DefaultBroadcastPort = 52913;

    public const int BroadcastIntervalMs = 2000;

    public const string DefaultMetadataDirectory = ".transfers_in_progress";


    public enum ByteSuffix
    {
        B = 0,
        KB,
        MB,
        GB,
        TB,
        PB,
        EB,
    }

    public static string AddHighestPossibleByteSuffixToNumber(long number)
    {

        ByteSuffix suffix = GetHighestPossibleByteSuffixForNumber(number);
        return AddByteSuffixToNumber(number, suffix);
    }

    public static string AddByteSuffixToNumber(long number, ByteSuffix suffix)
        => $"{DivideNumberToFitSuffix(number, suffix):F2} {suffix}";

    public static ByteSuffix GetHighestPossibleByteSuffixForNumber(long number)
    {
        const long divisor = 1024;

        var suffix = ByteSuffix.B;
        while (number > divisor)
        {
            number /= divisor;
            ++suffix;
        }

        return suffix;
    }

    public static double DivideNumberToFitSuffix(long number, ByteSuffix suffix)
    {
        const double divisor = 1024;

        var numberFloatingPoint = (double)number;
        for (var i = 0; i < (int)suffix; ++i)
        {
            numberFloatingPoint /= divisor;
        }

        return numberFloatingPoint;
    }

    public static List<UnicastIPAddressInformation> FindAllLocalAddressInfo()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        return interfaces
            .Where(@interface => @interface.OperationalStatus == OperationalStatus.Up)
            .Where(@interface => @interface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(@interface => @interface.GetIPProperties().UnicastAddresses)
            .Where(addressInfo => addressInfo.Address.AddressFamily == AddressFamily.InterNetwork)
            .ToList();

    }

    public static IPAddress CalculateNetworkBroadcastAddress(UnicastIPAddressInformation addressInfo)
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
}