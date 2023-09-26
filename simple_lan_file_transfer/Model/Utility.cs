namespace simple_lan_file_transfer.Models;

/// <summary>
/// Class containing various utility methods and constants.
/// </summary>
internal static class Utility
{
    public const int  BytesInKiloByte = 1024;

    public const int BlockSizeKb = 64;
    public const int BlockSize = BlockSizeKb * BytesInKiloByte;

    public const int SocketBufferSizeKb = 128;
    public const int SocketBufferSize = SocketBufferSizeKb * BytesInKiloByte;

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

    /// <summary>
    /// Adds the highest possible suffix to the number, while keeping the number higher than 1.
    /// </summary>
    /// <param name="number">Number to add suffix to</param>
    /// <returns>
    /// A string containing the number with the highest possible suffix which still keeps the number higher than 1
    /// </returns>
    public static string AddHighestPossibleByteSuffixToNumber(long number)
    {

        ByteSuffix suffix = GetHighestPossibleByteSuffixForNumber(number);
        return AddByteSuffixToNumber(number, suffix);
    }

    /// <summary>
    /// Adds the specified suffix to the number, dividing it.
    /// </summary>
    /// <param name="number">Number to which the suffix will be added</param>
    /// <param name="suffix">The suffix to add</param>
    /// <returns></returns>
    public static string AddByteSuffixToNumber(long number, ByteSuffix suffix)
        => $"{DivideNumberToFitSuffix(number, suffix):F2} {suffix}";

    /// <summary>
    /// Calculate the highest possible suffix for the number, while keeping the number higher than 1.
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Divides the number to fit specified suffix.
    /// </summary>
    /// <param name="number">Number to be divided</param>
    /// <param name="suffix">Suffix to fit the number to</param>
    /// <returns></returns>
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

    /// <summary>
    /// Gets list of IP addresses for all operational network interfaces.
    /// </summary>
    /// <returns>List of IP addresses for all operational network interfaces</returns>
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

    /// <summary>
    /// Calculates the local network broadcast address for the specified address info.
    /// </summary>
    /// <param name="addressInfo">Address to calculate broadcast address for</param>
    /// <returns></returns>
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