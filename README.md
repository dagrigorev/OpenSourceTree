# OpenSourceTree

An open-source, cross-platform clone of [Atlassian SourceTree](https://www.sourcetreeapp.com/) built with **AvaloniaUI** (WPF-style XAML, runs on Windows / macOS / Linux), **C# / .NET 8**, **LibGit2Sharp** for local git operations and the system **git CLI** for network operations (so credential helpers and SSH agents work exactly like they do in SourceTree).

![History view with the commit graph](docs/screenshot.png)

## Features

- **Tabbed repositories** in the title bar (like SourceTree), persisted between sessions
- **History filters** — all/current branch combo, *show remote branches* toggle, date/topological sort, and a *Jump to* box (message, author or SHA)
- **Sidebar** with SourceTree-style section icons and chevrons plus a filter box that narrows branches, tags, remotes and stashes
- **Single-selection sidebar tree** — clicking a branch, tag or remote branch highlights the row, switches to History and scrolls to that ref's commit; Workspace nav items and sidebar rows share one selection like SourceTree
- **File-based SVG icons** — the original icon set lives in `Assets/Icons/*.svg`, copied next to the executable and loaded from disk at runtime (Avalonia.Svg.Skia), not from embedded assembly resources
- **Repository settings dialog** with *Remotes* (add/edit/remove remote paths) and *Advanced* (.gitignore editing, local vs. global user identity) tabs
- **Full menu bar** in SourceTree's order — File, Edit (undo/redo/cut/copy/paste/select-all on the focused text field), View, Repository, Actions, Tools (SSH agent, SSH keys, Options…) and Help (README, keyboard shortcuts, about)
- **Options dialog** (Tools → Options) with *General* (global git identity, default clone directory, reopen-tabs-on-startup, history commit limit), *Git* (custom git executable path + detected version) and *Diff* (context lines) tabs — all persisted and applied live
- **Pending files sorting** — by file status, path or file name, with file counts
- **Git Flow** — initialize (production/development branches + prefixes stored in git config, compatible with the git-flow tool), start feature/release/hotfix branches, and finish them with no-fast-forward merges, version tags and branch cleanup
- **Interactive rebase** — right-click a commit → *Rebase children interactively*: pick / reword / squash / drop and reorder in a dialog; runs as a native cherry-pick replay that automatically aborts and restores the branch on any conflict
- **Submodules** — sidebar section listing all submodules (double-click opens one as a tab), add submodule dialog, and init/update (single or recursive all) via the git CLI
- **History view** — commit log with a custom-drawn branch graph (lanes, merge edges, colored per branch), ref badges (HEAD / local / remote branches, tags), date / author / SHA columns
- **Commit details** — SHA, parents, author, refs, full message, changed files with status badges, per-file unified diff with line numbers and add/remove coloring
- **File Status view** — staged / unstaged lists with checkbox staging, Stage All / Unstage All, per-file diff, discard, commit box with author line, *amend* and *push immediately* options
- **Sidebar** — branches (current marker, ahead/behind counters), tags, remotes with remote branches, stashes; double-click to checkout, full context menus (checkout / merge / delete / apply / pop / drop)
- **Toolbar** — Commit, Pull, Push, Fetch, Branch, Merge, Stash, Discard, Tag, Terminal, Explorer, Settings
- **Network operations** (clone / fetch / pull / push) via the git CLI with live output window
- **Search view** — find commits by message, author or SHA
- **Commit context menu** — checkout commit, tag, reset (soft / mixed / hard), copy SHA
- **Repository settings** — user.name / user.email (local or global)
- Auto-refresh via filesystem watcher; F5 to refresh manually
- New tab page: clone a remote, open a local repo (with recents), or create a new repository

## Screenshots

**History** — commit graph, ref badges, commit details, changed files and the diff viewer:

![History with commit details and diff](docs/history-details.png)

**File status** — staged/unstaged lists with checkbox staging, per-file diff and the commit box:

![File status view](docs/filestatus.png)

**Options** (Tools → Options) — global identity, clone directory, history limit, git executable, diff context:

![Options dialog](docs/options.png)

## Building

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer.
For clone/fetch/pull/push a `git` executable must be on `PATH` (or set one in Tools → Options → Git).

### Quick start (any OS)

```bash
dotnet build OpenSourceTree.slnx
dotnet run --project src/OpenSourceTree
```

### Release packages

Self-contained builds (no .NET runtime needed on the target machine) land in `dist/<rid>/`.

**Windows**

```powershell
.\build.ps1                      # win-x64 -> dist\win-x64\OpenSourceTree.exe
.\build.ps1 -Runtime win-arm64   # Windows on ARM
.\build.ps1 -FrameworkDependent  # smaller output, requires installed .NET runtime
.\build.ps1 -BuildOnly           # plain build without publishing
```

**Linux / macOS**

```bash
./build.sh                # auto-detects linux-x64 / osx-x64 / osx-arm64
./build.sh linux-arm64    # explicit runtime identifier
./build.sh --build-only   # plain build without publishing
```

Cross-publishing works too — e.g. `./build.sh linux-x64` on a Windows machine produces a Linux build.

## Architecture

```
src/OpenSourceTree/
  Models/        plain records: commits, refs, file status, diff lines, graph rows
  Services/
    GitService     LibGit2Sharp wrapper (status, history, branches, tags, stashes, diffs, reset…)
    GraphBuilder   assigns commits to graph lanes, emits drawing segments per row
    GitCliService  fetch/pull/push/clone through the system git executable
    DiffParser     unified-diff text → displayable lines
    Ui             compact code-built modal dialogs (input, confirm, pick, output, settings)
    AppSettings    JSON persistence (%APPDATA%/OpenSourceTree)
    PlatformService open terminal / file manager per OS
  ViewModels/    MVVM (CommunityToolkit.Mvvm), one RepositoryViewModel per tab
  Views/         AXAML views + CommitGraphControl (custom-drawn graph cell)
```

## Not implemented (vs. real SourceTree)

Hunk/line-level staging, file blame/log, bookmarks window, custom actions, Mercurial.
Interactive rebase is limited to linear (merge-free) ranges. Contributions welcome.
