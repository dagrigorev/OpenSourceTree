using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenSourceTree.Models;
using OpenSourceTree.Services;

namespace OpenSourceTree.ViewModels;

public sealed partial class RepositoryViewModel : TabViewModelBase
{
    private readonly GitService _git;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FileSystemWatcher? _watcher;
    private readonly DispatcherTimer _watchDebounce;
    private List<CommitRowViewModel> _allHistory = new();

    public string RepoPath { get; }

    public RepositoryViewModel(string path)
    {
        _git = new GitService(path);
        RepoPath = _git.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Title = Path.GetFileName(RepoPath);
        if (string.IsNullOrEmpty(Title))
            Title = RepoPath;

        _watchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _watchDebounce.Tick += async (_, _) =>
        {
            _watchDebounce.Stop();
            if (_gate.CurrentCount > 0)
                await RefreshAsync();
        };
        StartWatcher();
        Loc.Changed += OnLanguageChanged;

        _ = RefreshAsync();
    }

    public override void OnClosed()
    {
        Loc.Changed -= OnLanguageChanged;
        _watcher?.Dispose();
        _watchDebounce.Stop();
        _git.Dispose();
    }

    private void StartWatcher()
    {
        try
        {
            _watcher = new FileSystemWatcher(RepoPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };
            FileSystemEventHandler handler = (_, e) =>
            {
                var p = e.FullPath;
                // ignore noisy internal churn, but react to ref/HEAD/index updates
                if (p.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                    !p.EndsWith("HEAD", StringComparison.OrdinalIgnoreCase) &&
                    !p.EndsWith("index", StringComparison.OrdinalIgnoreCase) &&
                    !p.Contains("refs", StringComparison.OrdinalIgnoreCase))
                    return;
                if (p.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
                    return;
                Dispatcher.UIThread.Post(() => { _watchDebounce.Stop(); _watchDebounce.Start(); });
            };
            _watcher.Changed += handler;
            _watcher.Created += handler;
            _watcher.Deleted += handler;
            _watcher.Renamed += (s, e) => handler(s, e);
            _watcher.EnableRaisingEvents = true;
        }
        catch
        {
            // watching is best-effort (e.g. network drives)
        }
    }

    // ---------- Sections ----------

    [ObservableProperty] private bool _isFileStatusSelected;
    [ObservableProperty] private bool _isHistorySelected = true;
    [ObservableProperty] private bool _isSearchSelected;

    // The workspace nav items only show as selected while no sidebar row (branch/tag/…)
    // holds the selection — mirroring SourceTree's single-selection left tree.
    public bool IsFileStatusNavSelected => IsFileStatusSelected && _selectedSidebar is null;
    public bool IsHistoryNavSelected => IsHistorySelected && _selectedSidebar is null;
    public bool IsSearchNavSelected => IsSearchSelected && _selectedSidebar is null;

    partial void OnIsFileStatusSelectedChanged(bool value) => NotifyNavSelection();
    partial void OnIsHistorySelectedChanged(bool value) => NotifyNavSelection();
    partial void OnIsSearchSelectedChanged(bool value) => NotifyNavSelection();

    private void NotifyNavSelection()
    {
        OnPropertyChanged(nameof(IsFileStatusNavSelected));
        OnPropertyChanged(nameof(IsHistoryNavSelected));
        OnPropertyChanged(nameof(IsSearchNavSelected));
    }

    [RelayCommand]
    private void SelectSection(string section)
    {
        ClearSidebarSelection();
        IsFileStatusSelected = section == "FileStatus";
        IsHistorySelected = section == "History";
        IsSearchSelected = section == "Search";
    }

    // ---------- Sidebar single selection ----------

    private ISidebarItem? _selectedSidebar;

    private void ClearSidebarSelection()
    {
        if (_selectedSidebar is null)
            return;
        _selectedSidebar.IsSelected = false;
        _selectedSidebar = null;
        NotifyNavSelection();
    }

    /// <summary>Single click on a sidebar row: select it and, like SourceTree, show its commit in History.</summary>
    public void SelectSidebarItem(ISidebarItem item)
    {
        if (!ReferenceEquals(_selectedSidebar, item))
        {
            if (_selectedSidebar is not null)
                _selectedSidebar.IsSelected = false;
            _selectedSidebar = item;
            item.IsSelected = true;
        }

        if (item.JumpSha is { } sha)
        {
            IsFileStatusSelected = false;
            IsHistorySelected = true;
            IsSearchSelected = false;
            var row = History.FirstOrDefault(r => r.Sha == sha);
            if (row is not null)
                SelectedCommit = row;
        }

        NotifyNavSelection();
    }

    private void RestoreSidebarSelection()
    {
        if (_selectedSidebar is null)
            return;
        var key = _selectedSidebar.SelectionKey;
        var match = (ISidebarItem?)_allBranches.FirstOrDefault(b => b.SelectionKey == key)
            ?? (ISidebarItem?)_allTags.FirstOrDefault(t => t.SelectionKey == key)
            ?? (ISidebarItem?)_allStashes.FirstOrDefault(s => s.SelectionKey == key)
            ?? _allRemotes.SelectMany(r => r.Branches).FirstOrDefault(b => b.SelectionKey == key);
        _selectedSidebar = match;
        if (match is not null)
            match.IsSelected = true;
        NotifyNavSelection();
    }

    // ---------- State ----------

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _headName = "";
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private bool _hasPending;

    partial void OnPendingCountChanged(int value) => HasPending = value > 0;

    public ObservableCollection<BranchItemViewModel> Branches { get; } = new();
    public ObservableCollection<RemoteItemViewModel> Remotes { get; } = new();
    public ObservableCollection<TagItemViewModel> Tags { get; } = new();
    public ObservableCollection<StashItemViewModel> Stashes { get; } = new();

    public ObservableCollection<CommitRowViewModel> History { get; } = new();
    [ObservableProperty] private double _graphColumnWidth = 60;
    [ObservableProperty] private CommitRowViewModel? _selectedCommit;

    public ObservableCollection<FileStatusItemViewModel> CommitFiles { get; } = new();
    [ObservableProperty] private FileStatusItemViewModel? _selectedCommitFile;
    public ObservableCollection<DiffLineViewModel> CommitDiffLines { get; } = new();
    [ObservableProperty] private string _detailSha = "";
    [ObservableProperty] private string _detailParents = "";
    [ObservableProperty] private string _detailAuthor = "";
    [ObservableProperty] private string _detailDate = "";
    [ObservableProperty] private string _detailRefs = "";
    [ObservableProperty] private string _detailMessage = "";
    [ObservableProperty] private bool _hasCommitSelected;

    public ObservableCollection<FileStatusItemViewModel> StagedFiles { get; } = new();
    public ObservableCollection<FileStatusItemViewModel> UnstagedFiles { get; } = new();
    [ObservableProperty] private FileStatusItemViewModel? _selectedStagedFile;
    [ObservableProperty] private FileStatusItemViewModel? _selectedUnstagedFile;
    public ObservableCollection<DiffLineViewModel> WorkDiffLines { get; } = new();

    [ObservableProperty] private string _commitMessage = "";
    [ObservableProperty] private bool _amendCommit;
    [ObservableProperty] private bool _pushAfterCommit;
    [ObservableProperty] private string _commitAuthorText = "";

    [ObservableProperty] private string _searchText = "";
    public ObservableCollection<CommitRowViewModel> SearchResults { get; } = new();

    // ---------- History filters / sorting (the boxes above the log) ----------

    /// <summary>0 = all branches, 1 = current branch only.</summary>
    [ObservableProperty] private int _branchFilterIndex;
    [ObservableProperty] private bool _showRemoteBranches = true;
    /// <summary>0 = sort by date, 1 = topological order.</summary>
    [ObservableProperty] private int _historySortIndex;
    [ObservableProperty] private string _jumpToText = "";

    /// <summary>Set while re-selecting combo indices after a language switch (no real change).</summary>
    private bool _suppressFilterRefresh;

    partial void OnBranchFilterIndexChanged(int value)
    {
        if (!_suppressFilterRefresh && value >= 0) _ = RefreshAsync();
    }

    partial void OnShowRemoteBranchesChanged(bool value)
    {
        if (!_suppressFilterRefresh) _ = RefreshAsync();
    }

    partial void OnHistorySortIndexChanged(int value)
    {
        if (!_suppressFilterRefresh && value >= 0) _ = RefreshAsync();
    }

    /// <summary>
    /// ComboBox selection boxes keep a snapshot of the selected item's text; after a language
    /// switch we bounce the indices so they re-render with the new strings.
    /// </summary>
    private void OnLanguageChanged()
    {
        _suppressFilterRefresh = true;
        try
        {
            int branch = BranchFilterIndex;
            BranchFilterIndex = -1;
            BranchFilterIndex = branch;
            int sort = HistorySortIndex;
            HistorySortIndex = -1;
            HistorySortIndex = sort;
            int files = FileSortIndex;
            FileSortIndex = -1;
            FileSortIndex = files;
        }
        finally
        {
            _suppressFilterRefresh = false;
        }
    }

    /// <summary>Selects the next history row matching the query (message, author or SHA prefix). Returns it for scrolling.</summary>
    public CommitRowViewModel? JumpToNext()
    {
        var query = JumpToText.Trim();
        if (query.Length == 0 || History.Count == 0)
            return null;
        int start = SelectedCommit is null ? 0 : History.IndexOf(SelectedCommit) + 1;
        for (int i = 0; i < History.Count; i++)
        {
            var row = History[(start + i) % History.Count];
            if (row.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                row.AuthorName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                row.Sha.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                SelectedCommit = row;
                return row;
            }
        }
        return null;
    }

    // ---------- Sidebar filter ----------

    [ObservableProperty] private string _sidebarFilter = "";

    private List<BranchItemViewModel> _allBranches = new();
    private List<TagItemViewModel> _allTags = new();
    private List<RemoteItemViewModel> _allRemotes = new();
    private List<StashItemViewModel> _allStashes = new();

    partial void OnSidebarFilterChanged(string value) => ApplySidebarFilter();

    private void ApplySidebarFilter()
    {
        var q = SidebarFilter.Trim();
        bool Match(string s) => q.Length == 0 || s.Contains(q, StringComparison.OrdinalIgnoreCase);

        Replace(Branches, _allBranches.Where(b => Match(b.Name)));
        Replace(Tags, _allTags.Where(t => Match(t.Name)));
        Replace(Remotes, q.Length == 0
            ? _allRemotes
            : _allRemotes
                .Select(r => r.Filtered(q))
                .Where(r => r.Branches.Count > 0 || Match(r.Name)));
        Replace(Stashes, _allStashes.Where(s => Match(s.Title)));
        Replace(Submodules, _allSubmodules.Where(s => Match(s.Name)));
    }

    // ---------- Pending files sorting ----------

    /// <summary>0 = by status, 1 = by path, 2 = by file name.</summary>
    [ObservableProperty] private int _fileSortIndex;
    private List<FileStatusEntry> _lastStatus = new();

    partial void OnFileSortIndexChanged(int value)
    {
        if (!_suppressFilterRefresh && value >= 0) ApplyFileSort();
    }

    private IEnumerable<FileStatusEntry> SortFiles(IEnumerable<FileStatusEntry> files) => FileSortIndex switch
    {
        1 => files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase),
        2 => files.OrderBy(f => Path.GetFileName(f.Path), StringComparer.OrdinalIgnoreCase),
        _ => files.OrderBy(f => f.Kind).ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
    };

    private void ApplyFileSort()
    {
        var stagedPath = SelectedStagedFile?.Path;
        var unstagedPath = SelectedUnstagedFile?.Path;
        Replace(StagedFiles, SortFiles(_lastStatus.Where(s => s.Staged)).Select(s => new FileStatusItemViewModel(s, this)));
        Replace(UnstagedFiles, SortFiles(_lastStatus.Where(s => !s.Staged)).Select(s => new FileStatusItemViewModel(s, this)));
        PendingCount = _lastStatus.Count;
        if (stagedPath is not null)
            SelectedStagedFile = StagedFiles.FirstOrDefault(f => f.Path == stagedPath);
        if (unstagedPath is not null)
            SelectedUnstagedFile = UnstagedFiles.FirstOrDefault(f => f.Path == unstagedPath);
        if (SelectedStagedFile is null && SelectedUnstagedFile is null)
            WorkDiffLines.Clear();
    }

    // ---------- Refresh ----------

    public async Task RefreshAsync()
    {
        await _gate.WaitAsync();
        IsBusy = true;
        try
        {
            var snapshot = await Task.Run(() =>
            {
                var commits = _git.GetHistory(
                    currentBranchOnly: BranchFilterIndex == 1,
                    includeRemotes: ShowRemoteBranches,
                    sortTopological: HistorySortIndex == 1,
                    maxCount: AppSettings.Instance.HistoryLimit);
                var graph = GraphBuilder.Build(commits);
                var rows = commits.Select((c, i) => new CommitRowViewModel(c, graph[i], this)).ToList();
                var sig = _git.GetSignature();
                return new
                {
                    Branches = _git.GetLocalBranches(),
                    Remotes = _git.GetRemotes(),
                    Tags = _git.GetTags(),
                    Stashes = _git.GetStashes(),
                    Submodules = _git.GetSubmodules(),
                    Status = _git.GetStatus(),
                    Rows = rows,
                    Head = _git.HeadName,
                    Author = $"{sig.Name} <{sig.Email}>"
                };
            });

            HeadName = snapshot.Head;
            CommitAuthorText = snapshot.Author;

            // Tab badge: how far the current branch is ahead of its upstream (like SourceTree).
            var currentBranch = snapshot.Branches.FirstOrDefault(b => b.IsCurrent);
            TabBadge = currentBranch is { Ahead: > 0 } ? $"{currentBranch.Ahead} ↑" : "";

            _allBranches = snapshot.Branches.Select(b => new BranchItemViewModel(b, this)).ToList();
            _allRemotes = snapshot.Remotes.Select(r => new RemoteItemViewModel(r, this)).ToList();
            _allTags = snapshot.Tags.Select(t => new TagItemViewModel(t, this)).ToList();
            _allStashes = snapshot.Stashes.Select(s => new StashItemViewModel(s, this)).ToList();
            _allSubmodules = snapshot.Submodules.Select(s => new SubmoduleItemViewModel(s, this)).ToList();
            ApplySidebarFilter();
            RestoreSidebarSelection();

            var selectedSha = SelectedCommit?.Sha;
            _allHistory = snapshot.Rows;
            Replace(History, snapshot.Rows);
            int maxLanes = snapshot.Rows.Count == 0 ? 1 : snapshot.Rows.Max(r => r.Graph.LaneCount);
            GraphColumnWidth = Math.Clamp(maxLanes, 1, 12) * 14 + 10;
            if (selectedSha is not null)
                SelectedCommit = History.FirstOrDefault(r => r.Sha == selectedSha);

            _lastStatus = snapshot.Status;
            ApplyFileSort();

            ApplySearch();
        }
        catch (Exception ex)
        {
            await Ui.ShowErrorAsync("Refresh failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    [RelayCommand]
    private Task Refresh() => RefreshAsync();

    // ---------- Operation plumbing ----------

    private async Task RunGitAsync(Action action, bool refresh = true)
    {
        await _gate.WaitAsync();
        IsBusy = true;
        try
        {
            await Task.Run(action);
        }
        catch (Exception ex)
        {
            await Ui.ShowErrorAsync("Git operation failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
        if (refresh)
            await RefreshAsync();
    }

    private async Task RunCliAsync(string title, Func<Action<string>, Task<int>> operation)
    {
        var output = Ui.ShowOutput($"{title} — {Title}");
        IsBusy = true;
        try
        {
            await operation(line => output.Append(line));
        }
        catch (Exception ex)
        {
            output.Append("ERROR: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
            output.Complete();
        }
        await RefreshAsync();
    }

    // ---------- Toolbar commands ----------

    [RelayCommand]
    private void ShowCommitView() => SelectSection("FileStatus");

    [RelayCommand]
    private Task Fetch() => RunCliAsync("Fetch", sink => GitCliService.FetchAsync(RepoPath, sink));

    [RelayCommand]
    private Task Pull() => RunCliAsync("Pull", sink => GitCliService.PullAsync(RepoPath, sink));

    [RelayCommand]
    private async Task Push()
    {
        string? branch = HeadName.StartsWith("HEAD detached") ? null : HeadName;
        await RunCliAsync("Push", sink => GitCliService.PushAsync(RepoPath, branch, sink));
    }

    [RelayCommand]
    private async Task NewBranch()
    {
        var (name, checkout) = await Ui.ShowInputAsync("New Branch", "Branch name:", "",
            "Checkout new branch", true);
        if (string.IsNullOrWhiteSpace(name))
            return;
        await RunGitAsync(() => _git.CreateBranch(name.Trim(), checkout));
    }

    [RelayCommand]
    private async Task Merge()
    {
        var candidates = Branches.Where(b => !b.IsCurrent).Select(b => b.Name).ToList();
        if (candidates.Count == 0)
        {
            await Ui.ShowErrorAsync("Merge", "There is no other branch to merge.");
            return;
        }
        var pick = await Ui.ShowPickAsync("Merge", $"Merge into '{HeadName}' from:", candidates);
        if (pick is null)
            return;
        string? result = null;
        await RunGitAsync(() => result = _git.Merge(pick));
        if (result is not null)
            await Ui.ShowInfoAsync("Merge", result);
    }

    [RelayCommand]
    private async Task StashChanges()
    {
        var (message, _) = await Ui.ShowInputAsync("Stash", "Stash message (optional):");
        if (message is null)
            return;
        await RunGitAsync(() => _git.StashSave(message));
    }

    [RelayCommand]
    private async Task DiscardAll()
    {
        if (UnstagedFiles.Count == 0)
            return;
        var ok = await Ui.ShowConfirmAsync("Discard changes",
            $"Discard ALL {UnstagedFiles.Count} unstaged change(s)? This cannot be undone.");
        if (!ok)
            return;
        var entries = UnstagedFiles.Select(f => f.Entry).ToList();
        await RunGitAsync(() => _git.DiscardChanges(entries));
    }

    [RelayCommand]
    private async Task NewTag()
    {
        var (name, _) = await Ui.ShowInputAsync("New Tag", "Tag name (created at HEAD):");
        if (string.IsNullOrWhiteSpace(name))
            return;
        await RunGitAsync(() => _git.CreateTag(name.Trim()));
    }

    [RelayCommand]
    private void OpenTerminal() => PlatformService.OpenTerminal(RepoPath);

    [RelayCommand]
    private void OpenExplorer() => PlatformService.OpenFileExplorer(RepoPath);

    [RelayCommand]
    private async Task OpenSettings()
    {
        var user = await Task.Run(_git.GetUserConfigEx);
        var remotes = await Task.Run(_git.GetRemoteList);
        var result = await Ui.ShowRepoSettingsAsync(Title, remotes, user, _git.GitIgnorePath);
        if (result is null)
            return;
        await RunGitAsync(() =>
        {
            foreach (var name in result.RemovedRemotes)
                _git.RemoveRemote(name);
            foreach (var (name, url) in result.UpdatedRemotes)
                _git.SetRemoteUrl(name, url);
            foreach (var (name, url) in result.AddedRemotes)
                _git.AddRemote(name, url);

            if (result.UseGlobalIdentity)
            {
                if (user.HasLocal)
                    _git.UnsetLocalUserConfig();
            }
            else
            {
                _git.SetUserConfig(result.LocalName, result.LocalEmail, global: false);
            }
        });
    }

    // ---------- Git Flow ----------

    [RelayCommand]
    private async Task GitFlow()
    {
        var cfg = await Task.Run(_git.GetGitFlowConfig);

        if (!cfg.IsInitialized)
        {
            var init = await Ui.ShowGitFlowInitAsync(cfg);
            if (init is null)
                return;
            await RunGitAsync(() => _git.InitGitFlow(init));
            return;
        }

        // Determine what "finish" would do for the current branch.
        string current = HeadName;
        string? finishLabel = null;
        if (current.StartsWith(cfg.FeaturePrefix, StringComparison.Ordinal))
            finishLabel = $"Finish Feature '{current[cfg.FeaturePrefix.Length..]}'";
        else if (current.StartsWith(cfg.ReleasePrefix, StringComparison.Ordinal))
            finishLabel = $"Finish Release '{current[cfg.ReleasePrefix.Length..]}'";
        else if (current.StartsWith(cfg.HotfixPrefix, StringComparison.Ordinal))
            finishLabel = $"Finish Hotfix '{current[cfg.HotfixPrefix.Length..]}'";

        var action = await Ui.ShowGitFlowHubAsync(current, finishLabel);
        if (action is null)
            return;

        switch (action)
        {
            case "feature":
            case "release":
            case "hotfix":
            {
                string prefix = action switch
                {
                    "feature" => cfg.FeaturePrefix,
                    "release" => cfg.ReleasePrefix,
                    _ => cfg.HotfixPrefix
                };
                string baseBranch = action == "hotfix" ? cfg.Master : cfg.Develop;
                var (name, _) = await Ui.ShowInputAsync($"Start {action}", $"Name (branch will be {prefix}<name>):");
                if (string.IsNullOrWhiteSpace(name))
                    return;
                await RunGitAsync(() => _git.GitFlowStart(prefix + name.Trim(), baseBranch));
                break;
            }
            case "finish":
            {
                string? message = null;
                if (current.StartsWith(cfg.FeaturePrefix, StringComparison.Ordinal))
                {
                    await RunGitAsync(() => message = _git.GitFlowFinishFeature(current));
                }
                else
                {
                    string prefix = current.StartsWith(cfg.ReleasePrefix, StringComparison.Ordinal)
                        ? cfg.ReleasePrefix : cfg.HotfixPrefix;
                    string version = current[prefix.Length..];
                    await RunGitAsync(() => message = _git.GitFlowFinishReleaseOrHotfix(current, version));
                }
                if (message is not null)
                    await Ui.ShowInfoAsync("Git Flow", message);
                break;
            }
        }
    }

    // ---------- Interactive rebase ----------

    public async Task RebaseInteractiveAsync(string baseSha)
    {
        bool blocked = await Task.Run(_git.HasBlockingChanges);
        if (blocked)
        {
            await Ui.ShowErrorAsync("Interactive rebase", "Commit or stash your changes first.");
            return;
        }

        List<CommitInfo> range;
        try
        {
            range = await Task.Run(() => _git.GetLinearRange(baseSha));
        }
        catch (Exception ex)
        {
            await Ui.ShowErrorAsync("Interactive rebase", ex.Message);
            return;
        }
        if (range.Count == 0)
        {
            await Ui.ShowInfoAsync("Interactive rebase", "There are no commits above the selected one.");
            return;
        }

        var steps = await Ui.ShowInteractiveRebaseAsync(range);
        if (steps is null)
            return;

        await RunGitAsync(() => _git.RunInteractiveRebase(baseSha, steps));
    }

    // ---------- Submodules ----------

    /// <summary>Set by MainWindowViewModel so repository tabs can open other repositories (e.g. submodules).</summary>
    public static Action<string>? OpenRepositoryHandler { get; set; }

    public ObservableCollection<SubmoduleItemViewModel> Submodules { get; } = new();
    private List<SubmoduleItemViewModel> _allSubmodules = new();

    [RelayCommand]
    private async Task AddSubmodule()
    {
        var entry = await Ui.ShowAddSubmoduleAsync();
        if (entry is null)
            return;
        await RunCliAsync("Add submodule",
            sink => GitCliService.RunAsync(RepoPath,
                $"submodule add \"{entry.Value.Url}\" \"{entry.Value.Path}\"", sink));
    }

    [RelayCommand]
    private Task UpdateSubmodules() =>
        RunCliAsync("Update submodules",
            sink => GitCliService.RunAsync(RepoPath, "submodule update --init --recursive", sink));

    public Task UpdateSubmoduleAsync(SubmoduleItemViewModel submodule) =>
        RunCliAsync($"Update {submodule.Name}",
            sink => GitCliService.RunAsync(RepoPath,
                $"submodule update --init --recursive -- \"{submodule.Info.Path}\"", sink));

    public void OpenSubmodule(SubmoduleItemViewModel submodule)
    {
        var full = Path.Combine(RepoPath, submodule.Info.Path);
        if (GitService.IsRepository(full))
            OpenRepositoryHandler?.Invoke(full);
        else
            _ = Ui.ShowErrorAsync("Submodule",
                $"'{submodule.Info.Path}' is not initialized yet. Run Update Submodules first.");
    }

    // ---------- Sidebar / item operations ----------

    public async Task CheckoutBranchAsync(BranchItemViewModel branch)
    {
        if (branch.IsCurrent)
            return;
        await RunGitAsync(() => _git.CheckoutBranch(branch.Name));
    }

    public async Task DeleteBranchAsync(BranchItemViewModel branch)
    {
        if (!await Ui.ShowConfirmAsync("Delete branch", $"Delete branch '{branch.Name}'?"))
            return;
        await RunGitAsync(() => _git.DeleteBranch(branch.Name));
    }

    public async Task MergeBranchAsync(BranchItemViewModel branch)
    {
        if (branch.IsCurrent)
            return;
        if (!await Ui.ShowConfirmAsync("Merge", $"Merge '{branch.Name}' into '{HeadName}'?"))
            return;
        string? result = null;
        await RunGitAsync(() => result = _git.Merge(branch.Name));
        if (result is not null)
            await Ui.ShowInfoAsync("Merge", result);
    }

    public Task CheckoutRemoteBranchAsync(RemoteBranchItemViewModel branch) =>
        RunGitAsync(() => _git.CheckoutBranch(branch.Info.FriendlyName));

    public async Task DeleteTagAsync(TagItemViewModel tag)
    {
        if (!await Ui.ShowConfirmAsync("Delete tag", $"Delete tag '{tag.Name}'?"))
            return;
        await RunGitAsync(() => _git.DeleteTag(tag.Name));
    }

    public Task ApplyStashAsync(StashItemViewModel stash, bool pop) =>
        RunGitAsync(() => _git.StashApply(stash.Info.Index, pop));

    public async Task DropStashAsync(StashItemViewModel stash)
    {
        if (!await Ui.ShowConfirmAsync("Delete stash", $"Delete stash '{stash.Title}'?"))
            return;
        await RunGitAsync(() => _git.StashDrop(stash.Info.Index));
    }

    public async Task CheckoutCommitAsync(string sha)
    {
        if (!await Ui.ShowConfirmAsync("Checkout commit",
                $"Checkout commit {sha[..8]}? Your working copy will be in a detached HEAD state."))
            return;
        await RunGitAsync(() => _git.CheckoutCommit(sha));
    }

    public async Task ResetToCommitAsync(string sha, string mode)
    {
        if (!await Ui.ShowConfirmAsync("Reset",
                $"Reset current branch to {sha[..8]} ({mode})?" +
                (mode == "hard" ? " All uncommitted changes will be lost." : "")))
            return;
        await RunGitAsync(() =>
        {
            switch (mode)
            {
                case "soft": _git.ResetSoft(sha); break;
                case "hard": _git.ResetHard(sha); break;
                default: _git.ResetMixed(sha); break;
            }
        });
    }

    public async Task TagCommitAsync(string sha)
    {
        var (name, _) = await Ui.ShowInputAsync("New Tag", $"Tag name for commit {sha[..8]}:");
        if (string.IsNullOrWhiteSpace(name))
            return;
        await RunGitAsync(() => _git.CreateTag(name.Trim(), sha));
    }

    public Task StageEntryAsync(FileStatusItemViewModel file) =>
        RunGitAsync(() => _git.Stage(new[] { file.Path }));

    public Task UnstageEntryAsync(FileStatusItemViewModel file) =>
        RunGitAsync(() => _git.Unstage(new[] { file.Path }));

    public async Task DiscardEntryAsync(FileStatusItemViewModel file)
    {
        if (!await Ui.ShowConfirmAsync("Discard changes",
                $"Discard changes to '{file.Path}'? This cannot be undone."))
            return;
        await RunGitAsync(() => _git.DiscardChanges(new[] { file.Entry }));
    }

    // ---------- History details ----------

    partial void OnSelectedCommitChanged(CommitRowViewModel? value)
    {
        HasCommitSelected = value is not null;
        CommitFiles.Clear();
        CommitDiffLines.Clear();
        SelectedCommitFile = null;
        if (value is null)
        {
            DetailSha = DetailParents = DetailAuthor = DetailDate = DetailRefs = DetailMessage = "";
            return;
        }

        var c = value.Commit;
        DetailSha = c.Sha;
        DetailParents = string.Join(", ", c.ParentShas.Select(p => p[..8]));
        DetailAuthor = $"{c.AuthorName} <{c.AuthorEmail}>";
        DetailDate = c.Date.LocalDateTime.ToString("dd MMMM yyyy HH:mm:ss");
        DetailRefs = string.Join(", ", c.Refs.Select(r => r.Name));
        DetailMessage = c.FullMessage.TrimEnd();

        _ = LoadCommitFilesAsync(c.Sha);
    }

    private async Task LoadCommitFilesAsync(string sha)
    {
        try
        {
            var files = await Task.Run(() => _git.GetCommitChanges(sha));
            if (SelectedCommit?.Sha != sha)
                return;
            Replace(CommitFiles, files.Select(f => new FileStatusItemViewModel(f)));
        }
        catch
        {
            // commit may have vanished after refresh
        }
    }

    partial void OnSelectedCommitFileChanged(FileStatusItemViewModel? value)
    {
        CommitDiffLines.Clear();
        if (value is null || SelectedCommit is null)
            return;
        _ = LoadCommitDiffAsync(SelectedCommit.Sha, value.Path);
    }

    private async Task LoadCommitDiffAsync(string sha, string path)
    {
        try
        {
            var text = await Task.Run(() => _git.GetCommitFileDiff(sha, path));
            if (SelectedCommit?.Sha != sha || SelectedCommitFile?.Path != path)
                return;
            Replace(CommitDiffLines, DiffParser.Parse(text).Select(l => new DiffLineViewModel(l)));
        }
        catch (Exception ex)
        {
            Replace(CommitDiffLines, new[] { new DiffLineViewModel(new DiffLine(DiffLineKind.FileHeader, ex.Message, null, null)) });
        }
    }

    // ---------- File status / commit ----------

    partial void OnSelectedStagedFileChanged(FileStatusItemViewModel? value)
    {
        if (value is null)
            return;
        SelectedUnstagedFile = null;
        _ = LoadWorkDiffAsync(value.Path, staged: true);
    }

    partial void OnSelectedUnstagedFileChanged(FileStatusItemViewModel? value)
    {
        if (value is null)
            return;
        SelectedStagedFile = null;
        _ = LoadWorkDiffAsync(value.Path, staged: false);
    }

    private async Task LoadWorkDiffAsync(string path, bool staged)
    {
        try
        {
            var text = await Task.Run(() => _git.GetWorkdirFileDiff(path, staged));
            Replace(WorkDiffLines, DiffParser.Parse(text).Select(l => new DiffLineViewModel(l)));
        }
        catch (Exception ex)
        {
            Replace(WorkDiffLines, new[] { new DiffLineViewModel(new DiffLine(DiffLineKind.FileHeader, ex.Message, null, null)) });
        }
    }

    [RelayCommand]
    private Task StageAll() => RunGitAsync(_git.StageAll);

    [RelayCommand]
    private Task UnstageAll() => RunGitAsync(_git.UnstageAll);

    partial void OnAmendCommitChanged(bool value)
    {
        if (value && string.IsNullOrWhiteSpace(CommitMessage))
            _ = PrefillAmendMessageAsync();
    }

    private async Task PrefillAmendMessageAsync()
    {
        var message = await Task.Run(() => _git.GetHeadCommitMessage());
        if (AmendCommit && string.IsNullOrWhiteSpace(CommitMessage) && message is not null)
            CommitMessage = message.TrimEnd();
    }

    [RelayCommand]
    private async Task Commit()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            await Ui.ShowErrorAsync("Commit", "Please enter a commit message.");
            return;
        }
        if (StagedFiles.Count == 0 && !AmendCommit)
        {
            await Ui.ShowErrorAsync("Commit", "There are no staged changes to commit.");
            return;
        }

        bool committed = false;
        string message = CommitMessage;
        bool amend = AmendCommit;
        await RunGitAsync(() =>
        {
            _git.CommitChanges(message, amend);
            committed = true;
        });
        if (!committed)
            return;

        CommitMessage = "";
        AmendCommit = false;

        if (PushAfterCommit)
            await Push();
    }

    // ---------- Search ----------

    partial void OnSearchTextChanged(string value) => ApplySearch();

    private void ApplySearch()
    {
        SearchResults.Clear();
        var query = SearchText?.Trim();
        if (string.IsNullOrEmpty(query))
            return;
        foreach (var row in _allHistory.Where(r =>
                     r.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     r.AuthorName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     r.Sha.StartsWith(query, StringComparison.OrdinalIgnoreCase)).Take(500))
        {
            SearchResults.Add(row);
        }
    }

    public void GoToCommit(CommitRowViewModel? row)
    {
        if (row is null)
            return;
        SelectSection("History");
        SelectedCommit = History.FirstOrDefault(r => r.Sha == row.Sha);
    }
}
