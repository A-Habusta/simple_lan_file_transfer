namespace simple_lan_file_transfer.Models;

internal static class Utility
{
    public const int BlockSizeKb = 32;
    public const int BlockSize = BlockSizeKb * 1024;

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

}