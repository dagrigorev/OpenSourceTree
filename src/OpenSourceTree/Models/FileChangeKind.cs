namespace OpenSourceTree.Models;

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
