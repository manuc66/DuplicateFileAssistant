# DuplicateFileAssistant
Utility tool to find duplicate files on the system.

The application is built on top of dotnet and works at least on Windows and Linux.

Different kind of file hashing are supported as well as fast file compare with SIMD.

<img title="Application current display" alt="Application current display" src="screenshot.png">

## Safety

- Duplicate candidates are verified byte-for-byte before being offered for deletion.
- Deleted files are moved to a configurable trash folder and can be restored from the
  `Trash` tab, including after an application restart.
- The trash manifest records the original path, size, timestamps, attributes, Unix mode
  and SHA-256 hash of every entry.
- Destructive operations are confirmed, and `Delete if elsewhere` / `Merge to destination`
  provide a dry-run mode.
- `Merge to destination` never overwrites conflicts; conflicting files are left untouched.
- The trash folder is excluded from scans, and directory symlinks/reparse points are not
  followed.

The trash is a safety net against manipulation errors, not a backup. Keep an independent
backup of irreplaceable files.

## Storage formats

- `trash-manifest.json` stores the mutable trash index as a JSON array.
- `operations.jsonl` is an append-only audit log. JSON Lines was chosen deliberately:
  it is simple, human-readable, dependency-free and well suited to an append-only log.

This tool is still under development, use it at your own risk.