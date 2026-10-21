namespace FileCompare;

public enum TrashBackend
{
    Application = 0,
    System = 1
}

public sealed record TrashEntry(
    string TrashedPath,
    string OriginalPath,
    long Length,
    DateTime LastWriteTimeUtc,
    DateTime CreationTimeUtc,
    string Sha256,
    FileAttributes Attributes,
    UnixFileMode? UnixFileMode,
    DateTime DeletedAtUtc,
    bool IsSymbolicLink)
{
    /// <summary>
    /// Backend that owns the entry. Older manifests do not contain this property and are
    /// therefore treated as application-trash entries.
    /// </summary>
    public TrashBackend Backend { get; init; } = TrashBackend.Application;

    public string SizeText => FileSizeFormatter.Format(Length);

    /// <summary>
    /// Optional OS-specific metadata file (an XDG <c>.trashinfo</c> file or a Windows
    /// <c>$I</c> record) that must be removed when the entry is restored or discarded.
    /// </summary>
    public string? SystemMetadataPath { get; init; }
}
