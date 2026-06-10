using System;
using System.Collections.Generic;

namespace OpenSourceTree.Models;

public enum RefKind
{
    Head,
    LocalBranch,
    RemoteBranch,
    Tag
}

public sealed record RefBadge(string Name, RefKind Kind);

public sealed class CommitInfo
{
    public required string Sha { get; init; }
    public string ShortSha => Sha.Length >= 8 ? Sha[..8] : Sha;
    public required string MessageShort { get; init; }
    public required string FullMessage { get; init; }
    public required string AuthorName { get; init; }
    public required string AuthorEmail { get; init; }
    public required DateTimeOffset Date { get; init; }
    public required IReadOnlyList<string> ParentShas { get; init; }
    public List<RefBadge> Refs { get; } = new();
}

public enum FileChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Untracked,
    Conflicted,
    TypeChanged,
    Unknown
}

public sealed record FileStatusEntry(string Path, FileChangeKind Kind, bool Staged, string? OldPath = null);

public sealed record BranchInfo(string Name, string FriendlyName, bool IsCurrent, string? TipSha, int Ahead, int Behind, string? TrackedBranch);

public sealed record RemoteBranchInfo(string Remote, string Name, string FriendlyName, string? TipSha);

public sealed record RemoteInfo(string Name, string Url, IReadOnlyList<RemoteBranchInfo> Branches);

public sealed record TagInfo(string Name, string TargetSha);

public sealed record StashInfo(int Index, string Message, DateTimeOffset Date);

public sealed record UserConfigInfo(string LocalName, string LocalEmail, string GlobalName, string GlobalEmail, bool HasLocal);

public sealed record GitFlowConfig(
    string Master,
    string Develop,
    string FeaturePrefix,
    string ReleasePrefix,
    string HotfixPrefix,
    string VersionTagPrefix,
    bool IsInitialized);

public enum RebaseAction
{
    Pick,
    Reword,
    Squash,
    Drop
}

public sealed record RebaseStep(string Sha, RebaseAction Action, string? NewMessage);

public sealed record SubmoduleInfo(string Name, string Path, string? HeadSha);

public sealed record RepoSettingsResult(
    List<(string Name, string Url)> AddedRemotes,
    List<(string Name, string Url)> UpdatedRemotes,
    List<string> RemovedRemotes,
    bool UseGlobalIdentity,
    string LocalName,
    string LocalEmail);

public enum DiffLineKind
{
    FileHeader,
    HunkHeader,
    Context,
    Added,
    Removed
}

public sealed record DiffLine(DiffLineKind Kind, string Text, int? OldLineNumber, int? NewLineNumber);

/// <summary>One drawn line segment inside a commit-graph cell. Lanes are column indices;
/// top-half segments run from the row's top edge to its vertical center, bottom-half
/// segments from the center to the bottom edge.</summary>
public sealed record GraphSegment(int FromLane, int ToLane, bool IsTopHalf, int ColorIndex);

public sealed class GraphRow
{
    public int NodeLane { get; init; }
    public int ColorIndex { get; init; }
    public int LaneCount { get; init; }
    public List<GraphSegment> Segments { get; } = new();
}
