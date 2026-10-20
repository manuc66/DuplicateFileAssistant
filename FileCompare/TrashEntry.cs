namespace FileCompare;

public sealed record TrashEntry(
    string TrashedPath,
    string OriginalPath,
    long Length,
    DateTime LastWriteTimeUtc,
    string Sha256,
    FileAttributes Attributes,
    DateTime DeletedAtUtc);
