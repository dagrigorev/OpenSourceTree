using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using OpenSourceTree.Models;

namespace OpenSourceTree.Services;

/// <summary>
/// Synchronous LibGit2Sharp wrapper for a single repository. Not thread-safe by itself;
/// callers funnel access through one background task at a time (see RepositoryViewModel).
/// </summary>
public sealed class GitService : IDisposable
{
    private readonly Repository _repo;

    public string WorkingDirectory { get; }
    public string RepoPath { get; }

    public GitService(string path)
    {
        var discovered = Repository.Discover(path)
            ?? throw new ArgumentException($"'{path}' is not a git repository.");
        _repo = new Repository(discovered);
        RepoPath = discovered;
        WorkingDirectory = _repo.Info.WorkingDirectory ?? path;
    }

    public static bool IsRepository(string path) => Repository.Discover(path) is not null;

    public static void Init(string path) => Repository.Init(path);

    public void Dispose() => _repo.Dispose();

    // ---------- General info ----------

    public string HeadName => _repo.Info.IsHeadDetached
        ? $"HEAD detached at {_repo.Head.Tip?.Sha[..8]}"
        : _repo.Head.FriendlyName;

    public string? HeadTipSha => _repo.Head.Tip?.Sha;

    public string? GetHeadCommitMessage() => _repo.Head.Tip?.Message;

    // ---------- Branches / remotes / tags / stashes ----------

    public List<BranchInfo> GetLocalBranches()
    {
        var result = new List<BranchInfo>();
        foreach (var b in _repo.Branches.Where(b => !b.IsRemote))
        {
            int ahead = b.TrackingDetails?.AheadBy ?? 0;
            int behind = b.TrackingDetails?.BehindBy ?? 0;
            result.Add(new BranchInfo(
                b.CanonicalName, b.FriendlyName, b.IsCurrentRepositoryHead,
                b.Tip?.Sha, ahead, behind, b.TrackedBranch?.FriendlyName));
        }
        return result.OrderByDescending(b => b.IsCurrent).ThenBy(b => b.FriendlyName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public List<RemoteInfo> GetRemotes()
    {
        var remoteBranches = _repo.Branches.Where(b => b.IsRemote).ToList();
        var result = new List<RemoteInfo>();
        foreach (var r in _repo.Network.Remotes)
        {
            var branches = remoteBranches
                .Where(b => b.FriendlyName.StartsWith(r.Name + "/", StringComparison.Ordinal))
                .Where(b => !b.FriendlyName.EndsWith("/HEAD", StringComparison.Ordinal))
                .Select(b => new RemoteBranchInfo(r.Name, b.FriendlyName[(r.Name.Length + 1)..], b.FriendlyName, b.Tip?.Sha))
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.Add(new RemoteInfo(r.Name, r.Url, branches));
        }
        return result;
    }

    public List<TagInfo> GetTags() =>
        _repo.Tags
            .Select(t => new TagInfo(t.FriendlyName, t.PeeledTarget?.Sha ?? t.Target.Sha))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public List<StashInfo> GetStashes() =>
        _repo.Stashes
            .Select((s, i) => new StashInfo(i, s.Message, s.WorkTree.Author.When))
            .ToList();

    // ---------- Status ----------

    public List<FileStatusEntry> GetStatus()
    {
        var entries = new List<FileStatusEntry>();
        var status = _repo.RetrieveStatus(new StatusOptions
        {
            IncludeUntracked = true,
            RecurseUntrackedDirs = true,
            IncludeIgnored = false
        });

        foreach (var e in status)
        {
            var s = e.State;
            if (s == FileStatus.Ignored) continue;

            if (s.HasFlag(FileStatus.Conflicted))
            {
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.Conflicted, Staged: false));
                continue;
            }

            // Staged half
            if (s.HasFlag(FileStatus.NewInIndex))
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.Added, true));
            else if (s.HasFlag(FileStatus.ModifiedInIndex))
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.Modified, true));
            else if (s.HasFlag(FileStatus.DeletedFromIndex))
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.Deleted, true));
            else if (s.HasFlag(FileStatus.RenamedInIndex))
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.Renamed, true,
                    e.HeadToIndexRenameDetails?.OldFilePath));
            else if (s.HasFlag(FileStatus.TypeChangeInIndex))
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.TypeChanged, true));

            // Unstaged half
            if (s.HasFlag(FileStatus.NewInWorkdir))
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.Untracked, false));
            else if (s.HasFlag(FileStatus.ModifiedInWorkdir))
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.Modified, false));
            else if (s.HasFlag(FileStatus.DeletedFromWorkdir))
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.Deleted, false));
            else if (s.HasFlag(FileStatus.RenamedInWorkdir))
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.Renamed, false,
                    e.IndexToWorkDirRenameDetails?.OldFilePath));
            else if (s.HasFlag(FileStatus.TypeChangeInWorkdir))
                entries.Add(new FileStatusEntry(e.FilePath, FileChangeKind.TypeChanged, false));
        }

        return entries.OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Stage(IEnumerable<string> paths) => Commands.Stage(_repo, paths);

    public void StageAll() => Commands.Stage(_repo, "*");

    public void Unstage(IEnumerable<string> paths) => Commands.Unstage(_repo, paths);

    public void UnstageAll() => Commands.Unstage(_repo, "*");

    public void DiscardChanges(IReadOnlyList<FileStatusEntry> entries)
    {
        var tracked = entries.Where(e => e.Kind != FileChangeKind.Untracked).Select(e => e.Path).ToList();
        if (tracked.Count > 0)
        {
            _repo.CheckoutPaths(_repo.Head.FriendlyName, tracked,
                new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
        }
        foreach (var e in entries.Where(e => e.Kind == FileChangeKind.Untracked))
        {
            var full = Path.Combine(WorkingDirectory, e.Path);
            if (File.Exists(full)) File.Delete(full);
        }
    }

    // ---------- Commit ----------

    public Signature GetSignature()
    {
        var sig = _repo.Config.BuildSignature(DateTimeOffset.Now);
        return sig ?? new Signature("Unknown", "unknown@localhost", DateTimeOffset.Now);
    }

    public string CommitChanges(string message, bool amend)
    {
        var sig = GetSignature();
        var commit = _repo.Commit(message, sig, sig, new CommitOptions { AmendPreviousCommit = amend });
        return commit.Sha;
    }

    // ---------- History ----------

    public List<CommitInfo> GetHistory(bool currentBranchOnly = false, bool includeRemotes = true,
        bool sortTopological = true, int maxCount = 3000)
    {
        IEnumerable<Branch> branches = _repo.Branches
            .Where(b => !b.FriendlyName.EndsWith("/HEAD", StringComparison.Ordinal));
        if (!includeRemotes)
            branches = branches.Where(b => !b.IsRemote);
        if (currentBranchOnly)
            branches = Enumerable.Empty<Branch>();

        var filter = new CommitFilter
        {
            SortBy = sortTopological
                ? CommitSortStrategies.Topological | CommitSortStrategies.Time
                : CommitSortStrategies.Time,
            IncludeReachableFrom = branches
                .Select(b => b.CanonicalName)
                .Cast<object>()
                .Append(_repo.Head.CanonicalName)
                .ToList()
        };

        var commits = new List<CommitInfo>();
        foreach (var c in _repo.Commits.QueryBy(filter).Take(maxCount))
            commits.Add(ToCommitInfo(c));

        AttachRefBadges(commits);
        return commits;
    }

    private static CommitInfo ToCommitInfo(Commit c) => new()
    {
        Sha = c.Sha,
        MessageShort = c.MessageShort,
        FullMessage = c.Message,
        AuthorName = c.Author.Name,
        AuthorEmail = c.Author.Email,
        Date = c.Author.When,
        ParentShas = c.Parents.Select(p => p.Sha).ToList()
    };

    private void AttachRefBadges(List<CommitInfo> commits)
    {
        var bySha = commits.ToDictionary(c => c.Sha);
        string? headTip = _repo.Head.Tip?.Sha;

        foreach (var b in _repo.Branches)
        {
            var sha = b.Tip?.Sha;
            if (sha is null || !bySha.TryGetValue(sha, out var c)) continue;
            if (b.IsRemote)
                c.Refs.Add(new RefBadge(b.FriendlyName, RefKind.RemoteBranch));
            else
                c.Refs.Add(new RefBadge(b.FriendlyName, b.IsCurrentRepositoryHead ? RefKind.Head : RefKind.LocalBranch));
        }

        foreach (var t in _repo.Tags)
        {
            var sha = t.PeeledTarget?.Sha ?? t.Target.Sha;
            if (bySha.TryGetValue(sha, out var c))
                c.Refs.Add(new RefBadge(t.FriendlyName, RefKind.Tag));
        }

        if (_repo.Info.IsHeadDetached && headTip is not null && bySha.TryGetValue(headTip, out var head))
            head.Refs.Insert(0, new RefBadge("HEAD", RefKind.Head));
    }

    // ---------- Diffs ----------

    private static CompareOptions DiffOptions => new()
    {
        ContextLines = Math.Clamp(AppSettings.Instance.DiffContextLines, 0, 100)
    };

    public List<FileStatusEntry> GetCommitChanges(string sha)
    {
        var commit = _repo.Lookup<Commit>(sha) ?? throw new ArgumentException($"Unknown commit {sha}");
        var parentTree = commit.Parents.FirstOrDefault()?.Tree;
        var changes = _repo.Diff.Compare<TreeChanges>(parentTree, commit.Tree);
        return changes.Select(ch => new FileStatusEntry(ch.Path, MapChangeKind(ch.Status), false, ch.OldPath != ch.Path ? ch.OldPath : null))
            .OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static FileChangeKind MapChangeKind(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => FileChangeKind.Added,
        ChangeKind.Deleted => FileChangeKind.Deleted,
        ChangeKind.Modified => FileChangeKind.Modified,
        ChangeKind.Renamed => FileChangeKind.Renamed,
        ChangeKind.TypeChanged => FileChangeKind.TypeChanged,
        ChangeKind.Conflicted => FileChangeKind.Conflicted,
        _ => FileChangeKind.Unknown
    };

    public string GetCommitFileDiff(string sha, string path)
    {
        var commit = _repo.Lookup<Commit>(sha) ?? throw new ArgumentException($"Unknown commit {sha}");
        var parentTree = commit.Parents.FirstOrDefault()?.Tree;
        var patch = _repo.Diff.Compare<Patch>(parentTree, commit.Tree, new[] { path }, compareOptions: DiffOptions);
        return patch.Content;
    }

    public string GetWorkdirFileDiff(string path, bool staged)
    {
        if (staged)
        {
            var patch = _repo.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.Index, new[] { path },
                null, DiffOptions);
            return patch.Content;
        }
        else
        {
            var patch = _repo.Diff.Compare<Patch>(new[] { path }, true, null, DiffOptions);
            return patch.Content;
        }
    }

    // ---------- Branch / tag / stash / merge operations ----------

    public void CreateBranch(string name, bool checkout)
    {
        var branch = _repo.CreateBranch(name);
        if (checkout)
            Commands.Checkout(_repo, branch);
    }

    public void CheckoutBranch(string friendlyName)
    {
        var branch = _repo.Branches[friendlyName]
            ?? throw new ArgumentException($"Branch '{friendlyName}' not found.");

        if (branch.IsRemote)
        {
            // Create (or reuse) a local tracking branch, like SourceTree's remote checkout.
            string localName = friendlyName[(friendlyName.IndexOf('/') + 1)..];
            var local = _repo.Branches[localName];
            if (local is null)
            {
                local = _repo.CreateBranch(localName, branch.Tip);
                _repo.Branches.Update(local, b => b.TrackedBranch = branch.CanonicalName);
            }
            Commands.Checkout(_repo, local);
        }
        else
        {
            Commands.Checkout(_repo, branch);
        }
    }

    public void CheckoutCommit(string sha)
    {
        var commit = _repo.Lookup<Commit>(sha) ?? throw new ArgumentException($"Unknown commit {sha}");
        Commands.Checkout(_repo, commit);
    }

    public void DeleteBranch(string friendlyName)
    {
        var branch = _repo.Branches[friendlyName] ?? throw new ArgumentException($"Branch '{friendlyName}' not found.");
        if (branch.IsCurrentRepositoryHead)
            throw new InvalidOperationException("Cannot delete the current branch.");
        _repo.Branches.Remove(branch);
    }

    public string Merge(string friendlyName)
    {
        var branch = _repo.Branches[friendlyName] ?? throw new ArgumentException($"Branch '{friendlyName}' not found.");
        var result = _repo.Merge(branch, GetSignature());
        return result.Status switch
        {
            MergeStatus.UpToDate => "Already up to date.",
            MergeStatus.FastForward => "Fast-forward merge completed.",
            MergeStatus.NonFastForward => $"Merge commit created: {result.Commit?.Sha[..8]}",
            MergeStatus.Conflicts => "Merge resulted in conflicts. Resolve them and commit.",
            _ => result.Status.ToString()
        };
    }

    public void CreateTag(string name, string? sha = null)
    {
        if (sha is null)
            _repo.ApplyTag(name);
        else
            _repo.ApplyTag(name, sha);
    }

    public void DeleteTag(string name) => _repo.Tags.Remove(name);

    public void StashSave(string message)
    {
        _repo.Stashes.Add(GetSignature(), string.IsNullOrWhiteSpace(message) ? null : message,
            StashModifiers.IncludeUntracked);
    }

    public void StashApply(int index, bool pop)
    {
        if (pop) _repo.Stashes.Pop(index);
        else _repo.Stashes.Apply(index);
    }

    public void StashDrop(int index) => _repo.Stashes.Remove(index);

    public void ResetHard(string sha) =>
        _repo.Reset(ResetMode.Hard, _repo.Lookup<Commit>(sha));

    public void ResetSoft(string sha) =>
        _repo.Reset(ResetMode.Soft, _repo.Lookup<Commit>(sha));

    public void ResetMixed(string sha) =>
        _repo.Reset(ResetMode.Mixed, _repo.Lookup<Commit>(sha));

    // ---------- Git Flow ----------

    public GitFlowConfig GetGitFlowConfig()
    {
        string master = _repo.Config.Get<string>("gitflow.branch.master")?.Value ?? "";
        string develop = _repo.Config.Get<string>("gitflow.branch.develop")?.Value ?? "";
        bool initialized = master.Length > 0 && develop.Length > 0;

        if (!initialized)
        {
            master = _repo.Branches["main"] is not null ? "main"
                : _repo.Branches["master"] is not null ? "master"
                : _repo.Head.FriendlyName;
            develop = "develop";
        }

        return new GitFlowConfig(
            master,
            develop,
            _repo.Config.Get<string>("gitflow.prefix.feature")?.Value ?? "feature/",
            _repo.Config.Get<string>("gitflow.prefix.release")?.Value ?? "release/",
            _repo.Config.Get<string>("gitflow.prefix.hotfix")?.Value ?? "hotfix/",
            _repo.Config.Get<string>("gitflow.prefix.versiontag")?.Value ?? "",
            initialized);
    }

    public void InitGitFlow(GitFlowConfig cfg)
    {
        var master = _repo.Branches[cfg.Master]
            ?? throw new InvalidOperationException($"Production branch '{cfg.Master}' does not exist.");
        var develop = _repo.Branches[cfg.Develop] ?? _repo.CreateBranch(cfg.Develop, master.Tip);

        _repo.Config.Set("gitflow.branch.master", cfg.Master, ConfigurationLevel.Local);
        _repo.Config.Set("gitflow.branch.develop", cfg.Develop, ConfigurationLevel.Local);
        _repo.Config.Set("gitflow.prefix.feature", cfg.FeaturePrefix, ConfigurationLevel.Local);
        _repo.Config.Set("gitflow.prefix.release", cfg.ReleasePrefix, ConfigurationLevel.Local);
        _repo.Config.Set("gitflow.prefix.hotfix", cfg.HotfixPrefix, ConfigurationLevel.Local);
        _repo.Config.Set("gitflow.prefix.versiontag", cfg.VersionTagPrefix, ConfigurationLevel.Local);

        Commands.Checkout(_repo, develop);
    }

    public void GitFlowStart(string newBranch, string baseBranch)
    {
        var b = _repo.Branches[baseBranch]
            ?? throw new InvalidOperationException($"Base branch '{baseBranch}' does not exist.");
        if (_repo.Branches[newBranch] is not null)
            throw new InvalidOperationException($"Branch '{newBranch}' already exists.");
        int slash = newBranch.IndexOf('/');
        if (slash > 0 && _repo.Branches[newBranch[..slash]] is not null)
            throw new InvalidOperationException(
                $"A branch named '{newBranch[..slash]}' exists, which blocks creating '{newBranch}'. " +
                "Delete or rename that branch first.");
        var nb = _repo.CreateBranch(newBranch, b.Tip);
        Commands.Checkout(_repo, nb);
    }

    private void MergeNoFF(string targetBranch, string sourceBranch)
    {
        Commands.Checkout(_repo, _repo.Branches[targetBranch]
            ?? throw new InvalidOperationException($"Branch '{targetBranch}' does not exist."));
        var result = _repo.Merge(
            _repo.Branches[sourceBranch] ?? throw new InvalidOperationException($"Branch '{sourceBranch}' does not exist."),
            GetSignature(),
            new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastForward });
        if (result.Status == MergeStatus.Conflicts)
            throw new InvalidOperationException(
                $"Merging '{sourceBranch}' into '{targetBranch}' produced conflicts. " +
                "Resolve them and commit; the flow branch was kept.");
    }

    public string GitFlowFinishFeature(string featureBranch)
    {
        var cfg = GetGitFlowConfig();
        MergeNoFF(cfg.Develop, featureBranch);
        _repo.Branches.Remove(featureBranch);
        return $"Feature '{featureBranch}' was merged into '{cfg.Develop}' and deleted.";
    }

    public string GitFlowFinishReleaseOrHotfix(string branch, string version)
    {
        var cfg = GetGitFlowConfig();
        MergeNoFF(cfg.Master, branch);
        string tag = cfg.VersionTagPrefix + version;
        if (_repo.Tags[tag] is null)
            _repo.ApplyTag(tag);
        MergeNoFF(cfg.Develop, branch);
        _repo.Branches.Remove(branch);
        Commands.Checkout(_repo, _repo.Branches[cfg.Develop]!);
        return $"'{branch}' was merged into '{cfg.Master}' and '{cfg.Develop}', tagged '{tag}', and deleted.";
    }

    // ---------- Interactive rebase (cherry-pick based, linear history) ----------

    /// <summary>Commits between <paramref name="baseSha"/> (exclusive) and HEAD, oldest first.</summary>
    public List<CommitInfo> GetLinearRange(string baseSha)
    {
        var commits = new List<CommitInfo>();
        var c = _repo.Head.Tip ?? throw new InvalidOperationException("Repository has no commits.");
        while (c.Sha != baseSha)
        {
            var parents = c.Parents.ToList();
            if (parents.Count > 1)
                throw new InvalidOperationException(
                    "The selected range contains merge commits — interactive rebase supports linear history only.");
            if (parents.Count == 0)
                throw new InvalidOperationException($"Commit {baseSha[..8]} is not an ancestor of HEAD.");
            commits.Add(ToCommitInfo(c));
            if (commits.Count > 500)
                throw new InvalidOperationException("The range is too large (more than 500 commits).");
            c = parents[0];
        }
        commits.Reverse();
        return commits;
    }

    public bool HasBlockingChanges() =>
        _repo.RetrieveStatus(new StatusOptions { IncludeUntracked = true })
            .Any(e => e.State != FileStatus.Ignored && e.State != FileStatus.NewInWorkdir);

    /// <summary>
    /// Replays the commits above <paramref name="baseSha"/> according to <paramref name="steps"/>.
    /// On any conflict the branch is reset back to its original tip and nothing changes.
    /// </summary>
    public void RunInteractiveRebase(string baseSha, IReadOnlyList<RebaseStep> steps)
    {
        if (_repo.Info.IsHeadDetached)
            throw new InvalidOperationException("Checkout a branch before rebasing.");
        if (HasBlockingChanges())
            throw new InvalidOperationException("Commit or stash your changes before rebasing.");

        var originalTip = _repo.Head.Tip!.Sha;
        var baseCommit = _repo.Lookup<Commit>(baseSha)
            ?? throw new InvalidOperationException($"Unknown commit {baseSha}.");
        var committer = GetSignature();

        _repo.Reset(ResetMode.Hard, baseCommit);
        try
        {
            string lastMessage = "";
            Signature? lastAuthor = null;
            bool anyApplied = false;

            foreach (var step in steps)
            {
                if (step.Action == RebaseAction.Drop)
                    continue;

                var commit = _repo.Lookup<Commit>(step.Sha)
                    ?? throw new InvalidOperationException($"Unknown commit {step.Sha}.");
                var author = commit.Author;

                // "Squash" with nothing applied yet degrades to a plain pick.
                bool squash = step.Action == RebaseAction.Squash && anyApplied;

                if (step.Action == RebaseAction.Pick || (step.Action == RebaseAction.Squash && !squash))
                {
                    var result = _repo.CherryPick(commit, committer);
                    if (result.Status == CherryPickStatus.Conflicts)
                        throw new InvalidOperationException(ConflictMessage(commit));
                    lastMessage = commit.Message;
                    lastAuthor = author;
                    anyApplied = true;
                    continue;
                }

                // Reword / Squash: apply the tree without committing, then commit manually.
                _repo.CherryPick(commit, committer, new CherryPickOptions { CommitOnSuccess = false });
                if (_repo.Index.Conflicts.Any())
                    throw new InvalidOperationException(ConflictMessage(commit));

                string message;
                Signature commitAuthor;
                if (squash)
                {
                    message = lastMessage.TrimEnd() + "\n\n" + (step.NewMessage ?? commit.Message).TrimEnd() + "\n";
                    commitAuthor = lastAuthor ?? author;
                }
                else // reword
                {
                    message = string.IsNullOrWhiteSpace(step.NewMessage) ? commit.Message : step.NewMessage!;
                    commitAuthor = author;
                }

                try
                {
                    _repo.Commit(message, commitAuthor, committer,
                        new CommitOptions { AmendPreviousCommit = squash });
                }
                catch (EmptyCommitException)
                {
                    // commit became empty (e.g. identical content) — skip it like git does
                }
                CleanupSequencerState();

                lastMessage = message;
                lastAuthor = commitAuthor;
                anyApplied = true;
            }
        }
        catch
        {
            CleanupSequencerState();
            _repo.Reset(ResetMode.Hard, _repo.Lookup<Commit>(originalTip));
            throw;
        }
    }

    private static string ConflictMessage(Commit commit) =>
        $"Applying '{commit.MessageShort}' ({commit.Sha[..8]}) produced conflicts. " +
        "The rebase was aborted and your branch is unchanged.";

    private void CleanupSequencerState()
    {
        foreach (var file in new[] { "CHERRY_PICK_HEAD", "MERGE_MSG", "sequencer" })
        {
            var p = Path.Combine(RepoPath, file);
            try
            {
                if (File.Exists(p)) File.Delete(p);
                else if (Directory.Exists(p)) Directory.Delete(p, true);
            }
            catch
            {
                // state cleanup is best-effort
            }
        }
    }

    // ---------- Submodules ----------

    public List<SubmoduleInfo> GetSubmodules() =>
        _repo.Submodules
            .Select(s => new SubmoduleInfo(s.Name, s.Path, s.HeadCommitId?.Sha))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    // ---------- Remote management ----------

    public List<(string Name, string Url)> GetRemoteList() =>
        _repo.Network.Remotes.Select(r => (r.Name, r.Url)).ToList();

    public void AddRemote(string name, string url) => _repo.Network.Remotes.Add(name, url);

    public void SetRemoteUrl(string name, string url) =>
        _repo.Network.Remotes.Update(name, r => r.Url = url);

    public void RemoveRemote(string name) => _repo.Network.Remotes.Remove(name);

    // ---------- Config ----------

    public string GitIgnorePath => Path.Combine(WorkingDirectory, ".gitignore");

    public (string Name, string Email) GetUserConfig()
    {
        var name = _repo.Config.Get<string>("user.name")?.Value ?? "";
        var email = _repo.Config.Get<string>("user.email")?.Value ?? "";
        return (name, email);
    }

    /// <summary>Effective, local-only and global-only user identity plus whether a local override exists.</summary>
    public UserConfigInfo GetUserConfigEx()
    {
        string localName = _repo.Config.Get<string>("user.name", ConfigurationLevel.Local)?.Value ?? "";
        string localEmail = _repo.Config.Get<string>("user.email", ConfigurationLevel.Local)?.Value ?? "";
        string globalName = _repo.Config.Get<string>("user.name", ConfigurationLevel.Global)?.Value ?? "";
        string globalEmail = _repo.Config.Get<string>("user.email", ConfigurationLevel.Global)?.Value ?? "";
        bool hasLocal = localName.Length > 0 || localEmail.Length > 0;
        return new UserConfigInfo(localName, localEmail, globalName, globalEmail, hasLocal);
    }

    public void SetUserConfig(string name, string email, bool global)
    {
        var level = global ? ConfigurationLevel.Global : ConfigurationLevel.Local;
        _repo.Config.Set("user.name", name, level);
        _repo.Config.Set("user.email", email, level);
    }

    public void UnsetLocalUserConfig()
    {
        _repo.Config.Unset("user.name", ConfigurationLevel.Local);
        _repo.Config.Unset("user.email", ConfigurationLevel.Local);
    }

    /// <summary>Global (~/.gitconfig) identity, readable without an open repository.</summary>
    public static (string Name, string Email) GetGlobalUser()
    {
        using var cfg = Configuration.BuildFrom(default(string));
        return (cfg.Get<string>("user.name", ConfigurationLevel.Global)?.Value ?? "",
                cfg.Get<string>("user.email", ConfigurationLevel.Global)?.Value ?? "");
    }

    public static void SetGlobalUser(string name, string email)
    {
        using var cfg = Configuration.BuildFrom(default(string));
        if (!string.IsNullOrWhiteSpace(name))
            cfg.Set("user.name", name.Trim(), ConfigurationLevel.Global);
        if (!string.IsNullOrWhiteSpace(email))
            cfg.Set("user.email", email.Trim(), ConfigurationLevel.Global);
    }
}
