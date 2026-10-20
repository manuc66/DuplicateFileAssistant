using System.Globalization;

namespace FileCompare;

/// <summary>
/// Formats byte counts for the UI. Uses binary multiples but the familiar
/// KB/MB/GB labels, with at most one decimal.
/// </summary>
public static class FileSizeFormatter
{
    private static readonly string[] Units = { "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            bytes = 0;
        }

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        double value = bytes / 1024.0;
        int unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string format = value >= 10 ? "0" : "0.#";
        return $"{value.ToString(format, CultureInfo.InvariantCulture)} {Units[unit]}";
    }
}
