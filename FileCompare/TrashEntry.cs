namespace FileCompare;

public sealed record TrashEntry(
    string TrashedPath,
    string OriginalPath,
    long Length,
    DateTime LastWriteTimeUtc,
    DateTime CreationTimeUtc,
    string Sha256,
    FileAttributes Attributes,
    UnixFileMode? UnixFileMode,
    DateTime DeletedAtUtc);
