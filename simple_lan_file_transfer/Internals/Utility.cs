namespace simple_lan_file_transfer.Internals;

internal static class Utility
{
    public const int BlockSizeKb = 8;
    public const int BlockSize = BlockSizeKb * 1024; 
    
    public const ushort DefaultPort = 52123;
    public const ushort DefaultBroadcastPort = 52913;
    
    public const int BroadcastIntervalMs = 2000;
}
