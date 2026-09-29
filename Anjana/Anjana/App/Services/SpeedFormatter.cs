namespace Anjana.Services;

public static class SpeedFormatter
{
    private static readonly string[] ByteUnits = { "B/s", "KB/s", "MB/s", "GB/s" };
    private static readonly string[] BitUnits = { "bps", "Kbps", "Mbps", "Gbps" };
    private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB" };

    /// <summary>Compact, width-stable rate: at most 4 characters ("0", "12.4", "845", "1.2").</summary>
    public static (string Value, string Unit) Rate(double bytesPerSec, SpeedUnit unit)
    {
        bool bits = unit == SpeedUnit.Bits;
        double v = bits ? bytesPerSec * 8 : bytesPerSec;
        double step = bits ? 1000 : 1024;
        int i = 0;
        while (v >= 999.5 && i < 3) { v /= step; i++; }

        string text = i == 0 || v >= 99.95 ? Math.Round(v).ToString("0") : v.ToString("0.0");
        return (text, (bits ? BitUnits : ByteUnits)[i]);
    }

    public static string Size(long bytes)
    {
        double v = bytes; int i = 0;
        while (v >= 1024 && i < SizeUnits.Length - 1) { v /= 1024; i++; }
        return (i == 0 ? v.ToString("0") : v.ToString("0.0")) + " " + SizeUnits[i];
    }
}
