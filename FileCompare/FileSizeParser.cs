using System.Globalization;

namespace FileCompare;

/// <summary>
/// Parses a human-readable size such as "10 MB", "1.5 GB" or a plain number of
/// bytes. Binary multiples are used, matching the display formatter.
/// </summary>
public static class FileSizeParser
{
    private const long Kb = 1024;
    private const long Mb = Kb * 1024;
    private const long Gb = Mb * 1024;
    private const long Tb = Gb * 1024;
    private const long Pb = Tb * 1024;

    public static bool TryParse(string? text, out long bytes)
    {
        bytes = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string value = text.Trim().Replace(',', '.');
        int split = 0;

        while (split < value.Length && (char.IsDigit(value[split]) || value[split] == '.'))
        {
            split++;
        }

        if (split == 0 ||
            !double.TryParse(
                value[..split],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double number) ||
            number < 0)
        {
            return false;
        }

        string unit = value[split..].Trim().ToUpperInvariant();
        long multiplier = unit switch
        {
            "" or "B" => 1,
            "KB" or "KIB" => Kb,
            "MB" or "MIB" => Mb,
            "GB" or "GIB" => Gb,
            "TB" or "TIB" => Tb,
            "PB" or "PIB" => Pb,
            _ => -1
        };

        if (multiplier < 0)
        {
            return false;
        }

        double total = number * multiplier;

        if (total > long.MaxValue)
        {
            return false;
        }

        bytes = (long)Math.Round(total);
        return true;
    }
}
