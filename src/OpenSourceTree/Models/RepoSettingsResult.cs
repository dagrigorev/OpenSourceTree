namespace OpenSourceTree.Models;

public sealed record RepoSettingsResult(
    List<(string Name, string Url)> AddedRemotes,
    List<(string Name, string Url)> UpdatedRemotes,
    List<string> RemovedRemotes,
    bool UseGlobalIdentity,
    string LocalName,
    string LocalEmail);
