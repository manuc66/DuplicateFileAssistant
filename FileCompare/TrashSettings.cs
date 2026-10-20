using System.Text.Json.Serialization;

namespace FileCompare;

public enum TrashMode
{
    Application = 0,
    System = 1
}

/// <summary>
/// Where deleted files go. <see cref="TrashMode.Application"/> uses a folder owned by the
/// application; <see cref="TrashMode.System"/> delegates to the operating system's trash.
/// </summary>
public sealed record TrashSettings(
    [property: JsonPropertyName("trashMode")] TrashMode Mode,
    [property: JsonPropertyName("trashFolder")] string TrashFolder);
