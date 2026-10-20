using System.Text.Json.Serialization;

namespace FileCompare;

/// <summary>
/// The folder the application should open on the next launch.
/// </summary>
public sealed record LastFolderSettings(
    [property: JsonPropertyName("lastFolder")] string LastFolder);
