namespace OpenSourceTree.Models;

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
