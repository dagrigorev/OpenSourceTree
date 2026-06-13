namespace OpenSourceTree.Models;

public sealed class GraphRow
{
    public int NodeLane { get; init; }
    public int ColorIndex { get; init; }
    public int LaneCount { get; init; }
    public List<GraphSegment> Segments { get; } = new();
}
