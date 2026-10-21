namespace DuplicateFileAssistant.Cli;

internal sealed class CliOptions
{
    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase)
    {
        "json",
        "apply",
        "no-cache",
        "semantic",
        "keeper",
        "no-download",
        "prune-cache",
        "prune",
        "clear"
    };

    private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

    public static CliOptions Parse(IEnumerable<string> args)
    {
        CliOptions options = new();
        List<string> values = args.ToList();

        for (int index = 0; index < values.Count; index++)
        {
            string argument = values[index];

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string key;
            string? value = null;
            int equalsIndex = argument.IndexOf('=');

            if (equalsIndex > 0)
            {
                key = argument[2..equalsIndex];
                value = argument[(equalsIndex + 1)..];
            }
            else
            {
                key = argument[2..];

                if (!Flags.Contains(key) && index + 1 < values.Count && !values[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = values[++index];
                }
            }

            if (Flags.Contains(key))
            {
                options._flags.Add(key);
            }
            else if (value != null)
            {
                if (!options._values.TryGetValue(key, out List<string>? list))
                {
                    list = new List<string>();
                    options._values[key] = list;
                }

                list.Add(value);
            }
        }

        return options;
    }

    public bool HasFlag(string key)
    {
        return _flags.Contains(key);
    }

    public bool TryGetSingle(string key, out string? value)
    {
        value = GetSingle(key);
        return value != null;
    }

    public string? GetSingle(string key)
    {
        return _values.TryGetValue(key, out List<string>? values) && values.Count > 0 ? values[^1] : null;
    }

    public IReadOnlyList<string> GetMany(string key)
    {
        return _values.TryGetValue(key, out List<string>? values) ? values : Array.Empty<string>();
    }

    public int GetInt(string key, int fallback)
    {
        return int.TryParse(GetSingle(key), out int value) ? value : fallback;
    }

    public long GetLong(string key, long fallback)
    {
        return long.TryParse(GetSingle(key), out long value) ? value : fallback;
    }

    public double GetDouble(string key, double fallback)
    {
        return double.TryParse(
            GetSingle(key),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double value)
            ? value
            : fallback;
    }
}
