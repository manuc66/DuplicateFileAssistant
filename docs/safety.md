---
title: Safety and trash
nav_order: 6
---

# Safety and trash

The design assumption is that a duplicate finder will eventually be wrong about something,
and that a wrong deletion must be recoverable.

## Before a deletion

- **Byte-for-byte verification.** Content-hash groups are confirmed by comparing the
  candidate files directly, not only by trusting the hash. A hash collision or a file that
  changed between hashing and comparison does not become a deletion.
- **A keeper per group.** The keeper cannot be deleted from its row; another file has to be
  marked as keeper first.
- **Confirmation.** Single deletions ask before moving a file.
- **Dry run.** **Delete if elsewhere**, **Merge to destination**, the list tools and the
  multi-folder bulk action all have a dry-run mode.
- **No overwrite.** Merge never replaces an existing destination file. A conflict is left
  untouched and reported.
- **No symlinked directories.** Directory symbolic links and reparse points are not
  followed during scans, so a scan cannot escape the tree the user chose.
- **Trash excluded.** The application trash folder is added to the exclusions of every scan.
  In system mode the OS trash directories are excluded too.

## Where deleted files go

Every **file** deleted through the app or the CLI is moved to a trash, never unlinked.
(Empty directories are the exception: they are removed directly, after a confirmation,
because there is nothing to restore.)

The desktop app has two modes, chosen in the **Settings** tab:

| Mode | Default | Where the file goes | Restore |
|---|---|---|---|
| Application trash | `~/DuplicateTrash` | A folder owned by the app, with its manifest and audit log. | From the **Trash** tab, with hash verification. |
| System trash | OS default | Windows Recycle Bin, freedesktop XDG trash, or the macOS Trash. | From the **Trash** tab (the app keeps its own manifest) or from the OS. |

The CLI always uses an application trash folder; `--trash <dir>` changes it. It does not
read the desktop setting.

### One folder, not one per volume

The application trash is a single folder, by default on the volume that holds your home
directory. It is not created per volume. A plain move is attempted first. When the source
and the trash are on different filesystems, the file is copied, its metadata is copied, and
the source is deleted; if any step fails, the manifest entry is removed and the error is
reported. This works, but it costs time and temporary space on the trash volume, and a very
large file can fail there. The process needs write permission on both sides.

The system trash follows the OS conventions instead: the Windows Recycle Bin is per drive,
the freedesktop trash uses `~/.local/share/Trash` for the home volume and
`<volume>/.Trash-<uid>` (or `<volume>/.Trash/<uid>`) for other volumes, and macOS uses
`~/.Trash` plus `.Trashes/<uid>` on other volumes.

Inside a trash, every entry gets a unique timestamp-and-GUID prefix so two files with the
same name never collide.

## The manifest

`trash-manifest.json` is the mutable index of the trash, stored as a JSON array. In
application mode it lives in the trash folder. In system mode the app keeps it under its own
data directory; the operating system keeps its own metadata (`.trashinfo` on Linux, `$I`
records on Windows) next to the file. Both are removed on restore.

Each entry records:

- the trash path and the **original absolute path**;
- the length, creation time and last-write time;
- the file attributes, and the Unix mode on non-Windows systems;
- the **SHA-256 of the file** as it was when it was trashed;
- the deletion time in UTC;
- which backend owns the entry, and the OS metadata path when there is one.

The entry is written **before** the move in application mode, so a crash between the two
leaves a mapping that can still be inspected rather than an orphan nobody can place. In
system mode the OS chooses the destination, so the entry is written immediately after the
move. **Restore** moves the file back to the original path and refuses if something now
occupies it.

A corrupt manifest fails closed: the application reports it and leaves the JSON on disk for
inspection rather than treating every entry as absent.

## The audit log

`operations.jsonl` is an append-only audit log in [JSON Lines](https://jsonlines.org/)
format: one JSON object per line, no enclosing array.

JSON Lines was chosen deliberately. It is simple, human-readable, dependency-free, and
well suited to an append-only log: a process that dies mid-write damages at most the last
line, not the history before it. It records operations and failures, including failed
moves and hard-link replacements.

## Hard links

**Hardlink** in a duplicate group replaces a duplicate with a hard link to the keeper. The
bytes exist once, both paths work, and deleting either path does not remove the data until
the last link is gone. It requires a filesystem that supports hard links and fails across
filesystems; it is offered as an alternative to deleting when a program expects the file to
exist at both paths.

## What the trash is not

The trash is a safety net against manipulation errors, **not a backup**:

- In application mode it lives on one filesystem (your home volume by default) and shares
  its failure modes. In system mode it depends on the OS trash and can be emptied by the
  system or another application.
- It grows: nothing prunes it automatically.
- It is a plain directory. A user or a cleanup tool can delete it.
- Restore assumes the original path is free.

Keep an independent backup of irreplaceable files. The tool is still under development;
use it at your own risk.
