---
title: Command-line tool
nav_order: 4
---

# Command-line tool

`Cli` is a JSON-capable front end over the same comparison engine as the desktop app.
Destructive commands are **dry runs by default**; add `--apply` explicitly.

```bash
dotnet run --project Cli -- duplicates --path ~/Pictures --json
```

After publishing the CLI as a self-contained executable, the same command is
`Cli duplicates ...` (the project and assembly name).

## Commands

| Command | Purpose | Deletes with `--apply` |
|---|---|---|
| `duplicates` | Files with identical content. | Yes: keeps one file per group, trashes the rest. |
| `similar-images` | Near-identical pictures by perceptual hash, optionally with a semantic pass. | **Never.** Report-only. |
| `empty-files` | Zero-byte files. | Yes. |
| `big-files` | The largest files below the root (`--max`, default 100), with optional size and extension bounds. | Yes. |
| `temporary` | Temporary and backup files, with optional size and extension bounds. | Yes. |
| `invalid-symlinks` | Links whose target does not resolve. | Yes: the link, not the target. |
| `empty-folders` | Directories with no files. | Yes: removes the directories. |
| `cache` | Inspect, prune or clear the caches. | Only with `cache --clear --apply`. |

## Common options

| Option | Meaning |
|---|---|
| `--path <dir>` | Required root for every scan command. |
| `--json` | Print one JSON document instead of the human-readable report. |
| `--apply` | Actually perform the deletions. Without it, the command reports and exits. |
| `--trash <dir>` | Trash folder. Defaults to `~/DuplicateTrash` and is always excluded from the scan. |
| `--exclude <dir>` | Exclude another folder. Repeatable. |
| `--max <n>` | Maximum number of results for `big-files`. |
| `--min-size <size>` | Lower size bound for `big-files` and `temporary`, e.g. `10 MB` or `1024`. |
| `--max-size <size>` | Upper size bound for `big-files` and `temporary`. |
| `--extensions <ext,...>` | Extension filter for `big-files` and `temporary` (also used by `similar-images`). |
| `--algorithm sha256\|md5` | Content hash for `duplicates`. Defaults to `sha256`. |

Every destructive command supports `--apply`; `similar-images` rejects it with a message
rather than pretending to support it. Paths may start with `~`. Invalid paths, unknown
algorithms and malformed numeric options are rejected with an error instead of being
silently replaced by a default. Exclusions and `similar-images --extensions` accept both
`jpg` and `*.jpg`; a bare `*` means every extension.

## Examples

```bash
# Report duplicate groups as JSON
dotnet run --project Cli -- duplicates --path ~/Pictures --json

# Remove the extras, keeping the first path in each group
dotnet run --project Cli -- duplicates --path ~/Pictures --apply

# Large files, 20 at a time
dotnet run --project Cli -- big-files --path ~/Videos --max 20 --json

# Videos between 100 MB and 2 GB
dotnet run --project Cli -- big-files --path ~/Videos --min-size "100 MB" --max-size "2 GB" --extensions mp4,mkv

# Preview a cleanup, then apply it
dotnet run --project Cli -- temporary --path ~/Projects
dotnet run --project Cli -- temporary --path ~/Projects --apply

# Empty directories (no trash: the directory itself is removed)
dotnet run --project Cli -- empty-folders --path ~/Projects --apply
```

For `duplicates --apply`, the first path in sort order is kept and every other member of
the group is moved to the trash. This is deterministic, but it is not a judgement about
which copy is better. If that matters, use the desktop app or the similar-image keeper.

## JSON output

Every command emits one object. The shape is stable within a command but is not a
versioned public schema. `duplicates` reports the root, whether `--apply` was used, the
number of affected files, and one entry per group:

```json
{
  "command": "duplicates",
  "root": "/home/me/Pictures",
  "applied": false,
  "affected": 3,
  "results": [
    {
      "size": 248193,
      "hash": "…",
      "files": ["/home/me/Pictures/a.jpg", "/home/me/Pictures/copy/a.jpg"]
    }
  ]
}
```

`similar-images` adds the threshold, model ids, counts, elapsed time, and per-group
distances and similarities; see [Similar images](similar-images.md). Errors go to the same
stream as the report, so a caller should check the process exit code.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | The command ran. A dry run that found something still exits `0`. |
| `1` | Usage error, or a destructive action refused without `--apply`. |
| `2` | An exception was caught and reported. |

## Trash and logs

`--trash` defaults to `~/DuplicateTrash`, the same application folder the desktop app uses
by default. The CLI always uses an application trash; it does not read the desktop
**Settings** choice. A file is moved, not copied, so the operation needs write permission on
both the source and the trash. The manifest and audit log are described in
[Safety and trash](safety.md).
