using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.Input;
using OpenSourceTree.Models;

namespace OpenSourceTree.ViewModels;

/// <summary>A sidebar row that participates in the single-selection model (like SourceTree's left tree).</summary>
public interface ISidebarItem
{
    bool IsSelected { get; set; }
    /// <summary>Stable identity used to restore selection after a refresh rebuilds the items.</summary>
    string SelectionKey { get; }
    /// <summary>Commit the History view should jump to when this row is clicked (null = no jump).</summary>
    string? JumpSha { get; }
}

public sealed class RefBadgeViewModel
{
    public string Text { get; }
    public IBrush Background { get; }
    public IBrush Foreground { get; }

    private static readonly IBrush HeadBg = new ImmutableSolidColorBrush(Color.Parse("#2E74B5"));
    private static readonly IBrush LocalBg = new ImmutableSolidColorBrush(Color.Parse("#3A4A5A"));
    private static readonly IBrush RemoteBg = new ImmutableSolidColorBrush(Color.Parse("#52453A"));
    private static readonly IBrush TagBg = new ImmutableSolidColorBrush(Color.Parse("#B58A2E"));
    private static readonly IBrush Light = new ImmutableSolidColorBrush(Color.Parse("#E8EEF4"));
    private static readonly IBrush Dark = new ImmutableSolidColorBrush(Color.Parse("#141C24"));

    public RefBadgeViewModel(RefBadge badge)
    {
        Text = badge.Name;
        (Background, Foreground) = badge.Kind switch
        {
            RefKind.Head => (HeadBg, Light),
            RefKind.LocalBranch => (LocalBg, Light),
            RefKind.RemoteBranch => (RemoteBg, Light),
            RefKind.Tag => (TagBg, Dark),
            _ => (LocalBg, Light)
        };
    }
}

public sealed class CommitRowViewModel
{
    private readonly RepositoryViewModel _repo;

    public CommitInfo Commit { get; }
    public GraphRow Graph { get; }

    public string Sha => Commit.Sha;
    public string ShortSha => Commit.ShortSha;
    public string Message => Commit.MessageShort;
    public string AuthorName => Commit.AuthorName;
    public string DateText => Commit.Date.LocalDateTime.ToString("d MMM yyyy HH:mm");
    public List<RefBadgeViewModel> Badges { get; }
    public bool HasBadges => Badges.Count > 0;

    public ICommand CopyShaCommand { get; }
    public ICommand CheckoutCommand { get; }
    public ICommand ResetSoftCommand { get; }
    public ICommand ResetMixedCommand { get; }
    public ICommand ResetHardCommand { get; }
    public ICommand TagCommand { get; }
    public ICommand RebaseInteractiveCommand { get; }

    public CommitRowViewModel(CommitInfo commit, GraphRow graph, RepositoryViewModel repo)
    {
        Commit = commit;
        Graph = graph;
        _repo = repo;
        Badges = commit.Refs.Select(r => new RefBadgeViewModel(r)).ToList();

        CopyShaCommand = new RelayCommand(() => _ = Services.Ui.CopyToClipboardAsync(Sha));
        CheckoutCommand = new AsyncRelayCommand(() => _repo.CheckoutCommitAsync(Sha));
        ResetSoftCommand = new AsyncRelayCommand(() => _repo.ResetToCommitAsync(Sha, "soft"));
        ResetMixedCommand = new AsyncRelayCommand(() => _repo.ResetToCommitAsync(Sha, "mixed"));
        ResetHardCommand = new AsyncRelayCommand(() => _repo.ResetToCommitAsync(Sha, "hard"));
        TagCommand = new AsyncRelayCommand(() => _repo.TagCommitAsync(Sha));
        RebaseInteractiveCommand = new AsyncRelayCommand(() => _repo.RebaseInteractiveAsync(Sha));
    }
}

public sealed class FileStatusItemViewModel
{
    public FileStatusEntry Entry { get; }
    public string Path => Entry.Path;
    public string StatusChar { get; }
    public IBrush StatusBrush { get; }
    public bool Staged => Entry.Staged;

    public ICommand ToggleStageCommand { get; }
    public ICommand DiscardCommand { get; }

    private static readonly IBrush Green = new ImmutableSolidColorBrush(Color.Parse("#5CB85C"));
    private static readonly IBrush Orange = new ImmutableSolidColorBrush(Color.Parse("#E8A33D"));
    private static readonly IBrush Red = new ImmutableSolidColorBrush(Color.Parse("#D9534F"));
    private static readonly IBrush Blue = new ImmutableSolidColorBrush(Color.Parse("#5BC0DE"));
    private static readonly IBrush Purple = new ImmutableSolidColorBrush(Color.Parse("#B07CC6"));

    public FileStatusItemViewModel(FileStatusEntry entry, RepositoryViewModel? repo = null)
    {
        Entry = entry;
        (StatusChar, StatusBrush) = entry.Kind switch
        {
            FileChangeKind.Added => ("A", Green),
            FileChangeKind.Untracked => ("?", Purple),
            FileChangeKind.Modified => ("M", Orange),
            FileChangeKind.Deleted => ("D", Red),
            FileChangeKind.Renamed => ("R", Blue),
            FileChangeKind.Conflicted => ("!", Red),
            FileChangeKind.TypeChanged => ("T", Blue),
            _ => ("•", Orange)
        };

        ToggleStageCommand = new AsyncRelayCommand(() =>
            repo is null ? System.Threading.Tasks.Task.CompletedTask
                : Staged ? repo.UnstageEntryAsync(this) : repo.StageEntryAsync(this));
        DiscardCommand = new AsyncRelayCommand(() =>
            repo is null || Staged ? System.Threading.Tasks.Task.CompletedTask : repo.DiscardEntryAsync(this));
    }
}

public sealed class DiffLineViewModel
{
    public string Text { get; }
    public string OldNo { get; }
    public string NewNo { get; }
    public IBrush Background { get; }
    public IBrush Foreground { get; }

    private static readonly IBrush AddedBg = new ImmutableSolidColorBrush(Color.Parse("#1A3322"));
    private static readonly IBrush RemovedBg = new ImmutableSolidColorBrush(Color.Parse("#3A2022"));
    private static readonly IBrush HunkBg = new ImmutableSolidColorBrush(Color.Parse("#1E3245"));
    private static readonly IBrush HeaderBg = new ImmutableSolidColorBrush(Color.Parse("#1B2530"));
    private static readonly IBrush None = Brushes.Transparent;
    private static readonly IBrush NormalFg = new ImmutableSolidColorBrush(Color.Parse("#C9D4DF"));
    private static readonly IBrush DimFg = new ImmutableSolidColorBrush(Color.Parse("#6E8295"));
    private static readonly IBrush AddedFg = new ImmutableSolidColorBrush(Color.Parse("#7FD49B"));
    private static readonly IBrush RemovedFg = new ImmutableSolidColorBrush(Color.Parse("#E89A9A"));
    private static readonly IBrush HunkFg = new ImmutableSolidColorBrush(Color.Parse("#6FA8DC"));

    public DiffLineViewModel(DiffLine line)
    {
        OldNo = line.OldLineNumber?.ToString() ?? "";
        NewNo = line.NewLineNumber?.ToString() ?? "";

        switch (line.Kind)
        {
            case DiffLineKind.Added:
                Text = "+ " + line.Text;
                Background = AddedBg; Foreground = AddedFg;
                break;
            case DiffLineKind.Removed:
                Text = "- " + line.Text;
                Background = RemovedBg; Foreground = RemovedFg;
                break;
            case DiffLineKind.HunkHeader:
                Text = line.Text;
                Background = HunkBg; Foreground = HunkFg;
                break;
            case DiffLineKind.FileHeader:
                Text = line.Text;
                Background = HeaderBg; Foreground = DimFg;
                break;
            default:
                Text = "  " + line.Text;
                Background = None; Foreground = NormalFg;
                break;
        }
    }
}

public sealed partial class BranchItemViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, ISidebarItem
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isSelected;

    public string SelectionKey => "branch:" + Name;
    public string? JumpSha => Info.TipSha;

    public BranchInfo Info { get; }
    public string Name => Info.FriendlyName;
    public bool IsCurrent => Info.IsCurrent;
    public string AheadBehind { get; }
    public bool HasAheadBehind => AheadBehind.Length > 0;

    public ICommand CheckoutCommand { get; }
    public ICommand MergeCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CopyNameCommand { get; }

    public BranchItemViewModel(BranchInfo info, RepositoryViewModel repo)
    {
        Info = info;
        var parts = new List<string>();
        if (info.Ahead > 0) parts.Add($"↑{info.Ahead}");
        if (info.Behind > 0) parts.Add($"↓{info.Behind}");
        AheadBehind = string.Join(" ", parts);

        CheckoutCommand = new AsyncRelayCommand(() => repo.CheckoutBranchAsync(this));
        MergeCommand = new AsyncRelayCommand(() => repo.MergeBranchAsync(this));
        DeleteCommand = new AsyncRelayCommand(() => repo.DeleteBranchAsync(this));
        CopyNameCommand = new RelayCommand(() => _ = Services.Ui.CopyToClipboardAsync(Name));
    }
}

public sealed partial class RemoteBranchItemViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, ISidebarItem
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isSelected;

    public string SelectionKey => "remote:" + Info.FriendlyName;
    public string? JumpSha => Info.TipSha;

    public RemoteBranchInfo Info { get; }
    public string Name => Info.Name;

    public ICommand CheckoutCommand { get; }

    public RemoteBranchItemViewModel(RemoteBranchInfo info, RepositoryViewModel repo)
    {
        Info = info;
        CheckoutCommand = new AsyncRelayCommand(() => repo.CheckoutRemoteBranchAsync(this));
    }
}

public sealed class RemoteItemViewModel
{
    private readonly RepositoryViewModel _repo;

    public RemoteInfo Info { get; }
    public string Name => Info.Name;
    public string Url => Info.Url;
    public List<RemoteBranchItemViewModel> Branches { get; }

    public RemoteItemViewModel(RemoteInfo info, RepositoryViewModel repo)
    {
        _repo = repo;
        Info = info;
        Branches = info.Branches.Select(b => new RemoteBranchItemViewModel(b, repo)).ToList();
    }

    /// <summary>Copy of this remote with only the branches matching the sidebar filter.</summary>
    public RemoteItemViewModel Filtered(string query)
    {
        var info = new RemoteInfo(Info.Name, Info.Url,
            Info.Branches.Where(b => b.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList());
        return new RemoteItemViewModel(info, _repo);
    }
}

public sealed partial class TagItemViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, ISidebarItem
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isSelected;

    public string SelectionKey => "tag:" + Name;
    public string? JumpSha => Info.TargetSha;

    public TagInfo Info { get; }
    public string Name => Info.Name;

    public ICommand DeleteCommand { get; }

    public TagItemViewModel(TagInfo info, RepositoryViewModel repo)
    {
        Info = info;
        DeleteCommand = new AsyncRelayCommand(() => repo.DeleteTagAsync(this));
    }
}

public sealed partial class SubmoduleItemViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, ISidebarItem
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isSelected;

    public string SelectionKey => "submodule:" + Name;
    public string? JumpSha => null;

    public SubmoduleInfo Info { get; }
    public string Name => Info.Name;
    public string PathText => Info.Path;

    public ICommand OpenCommand { get; }
    public ICommand UpdateCommand { get; }

    public SubmoduleItemViewModel(SubmoduleInfo info, RepositoryViewModel repo)
    {
        Info = info;
        OpenCommand = new RelayCommand(() => repo.OpenSubmodule(this));
        UpdateCommand = new AsyncRelayCommand(() => repo.UpdateSubmoduleAsync(this));
    }
}

public sealed partial class StashItemViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, ISidebarItem
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isSelected;

    public string SelectionKey => "stash:" + Info.Index;
    public string? JumpSha => null;

    public StashInfo Info { get; }
    public string Title { get; }
    public string DateText { get; }

    public ICommand ApplyCommand { get; }
    public ICommand PopCommand { get; }
    public ICommand DropCommand { get; }

    public StashItemViewModel(StashInfo info, RepositoryViewModel repo)
    {
        Info = info;
        Title = info.Message;
        DateText = info.Date.LocalDateTime.ToString("d MMM yyyy HH:mm");

        ApplyCommand = new AsyncRelayCommand(() => repo.ApplyStashAsync(this, pop: false));
        PopCommand = new AsyncRelayCommand(() => repo.ApplyStashAsync(this, pop: true));
        DropCommand = new AsyncRelayCommand(() => repo.DropStashAsync(this));
    }
}
