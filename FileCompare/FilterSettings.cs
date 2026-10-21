using System.Text.Json.Serialization;

namespace FileCompare;

/// <summary>
/// Per-tab filter state. Empty size strings mean no bound; empty extensions mean
/// all files. Similar-image options are only used by that tab.
/// </summary>
public sealed record FilterSettings(
    [property: JsonPropertyName("extensions")] string Extensions,
    [property: JsonPropertyName("minimumSize")] string MinimumSizeText,
    [property: JsonPropertyName("maximumSize")] string MaximumSizeText,
    [property: JsonPropertyName("subFolder")] bool SubFolder,
    [property: JsonPropertyName("excludedFolders")] string ExcludedFolders,
    [property: JsonPropertyName("useSemantic")] bool UseSemantic,
    [property: JsonPropertyName("useKeeper")] bool UseKeeper,
    [property: JsonPropertyName("useKeeperModel")] bool UseKeeperModel,
    [property: JsonPropertyName("semanticThreshold")] double SemanticThreshold,
    [property: JsonPropertyName("allowModelDownload")] bool AllowModelDownload)
{
    public static FilterSettings Default { get; } = new(
        Extensions: string.Empty,
        MinimumSizeText: string.Empty,
        MaximumSizeText: string.Empty,
        SubFolder: true,
        ExcludedFolders: string.Empty,
        UseSemantic: false,
        UseKeeper: true,
        UseKeeperModel: false,
        SemanticThreshold: 0.8,
        AllowModelDownload: true);
}
