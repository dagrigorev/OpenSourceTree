using System;
using System.Collections.Generic;

namespace OpenSourceTree.Models;

public sealed record RefBadge(string Name, RefKind Kind);

public sealed record FileStatusEntry(string Path, FileChangeKind Kind, bool Staged, string? OldPath = null);

public sealed record BranchInfo(string Name, string FriendlyName, bool IsCurrent, string? TipSha, int Ahead, int Behind, string? TrackedBranch);

public sealed record RemoteBranchInfo(string Remote, string Name, string FriendlyName, string? TipSha);

public sealed record RemoteInfo(string Name, string Url, IReadOnlyList<RemoteBranchInfo> Branches);

public sealed record TagInfo(string Name, string TargetSha);

public sealed record StashInfo(int Index, string Message, DateTimeOffset Date);

public sealed record UserConfigInfo(string LocalName, string LocalEmail, string GlobalName, string GlobalEmail, bool HasLocal);

public sealed record RebaseStep(string Sha, RebaseAction Action, string? NewMessage);

public sealed record SubmoduleInfo(string Name, string Path, string? HeadSha);

public sealed record DiffLine(DiffLineKind Kind, string Text, int? OldLineNumber, int? NewLineNumber);

/// <summary>One drawn line segment inside a commit-graph cell. Lanes are column indices;
/// top-half segments run from the row's top edge to its vertical center, bottom-half
/// segments from the center to the bottom edge.</summary>
public sealed record GraphSegment(int FromLane, int ToLane, bool IsTopHalf, int ColorIndex);
