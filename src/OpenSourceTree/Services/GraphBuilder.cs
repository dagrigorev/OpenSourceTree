using System.Collections.Generic;
using System.Linq;
using OpenSourceTree.Models;

namespace OpenSourceTree.Services;

/// <summary>
/// Assigns commits (ordered newest-first, topologically) to graph lanes and produces
/// per-row drawing instructions, the way SourceTree renders its log graph.
/// </summary>
public static class GraphBuilder
{
    public static List<GraphRow> Build(IReadOnlyList<CommitInfo> commits)
    {
        var rows = new List<GraphRow>(commits.Count);
        // lanes[i] = sha of the commit expected to appear later in lane i (null = free slot)
        var lanes = new List<string?>();
        var laneColors = new List<int>();
        int nextColor = 0;

        foreach (var commit in commits)
        {
            var waiting = new List<int>();
            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i] == commit.Sha)
                    waiting.Add(i);
            }

            int nodeLane;
            if (waiting.Count == 0)
            {
                nodeLane = AllocateLane(lanes, laneColors, commit.Sha, nextColor++);
            }
            else
            {
                nodeLane = waiting[0];
            }

            int lanesBefore = lanes.Count;
            var row = new GraphRow
            {
                NodeLane = nodeLane,
                ColorIndex = laneColors[nodeLane],
                LaneCount = lanesBefore // patched below
            };

            // Incoming edges: every lane that was waiting for this commit bends into the node.
            foreach (var i in waiting)
                row.Segments.Add(new GraphSegment(i, nodeLane, IsTopHalf: true, laneColors[i]));

            // If this commit is a fresh tip its lane has no incoming edge, only outgoing.

            // Pass-through lanes continue straight across both halves of the row.
            for (int j = 0; j < lanes.Count; j++)
            {
                if (lanes[j] is null || waiting.Contains(j) || j == nodeLane)
                    continue;
                row.Segments.Add(new GraphSegment(j, j, IsTopHalf: true, laneColors[j]));
                row.Segments.Add(new GraphSegment(j, j, IsTopHalf: false, laneColors[j]));
            }

            // Lanes that merged into this node (other than the node lane) terminate here.
            foreach (var i in waiting.Skip(1))
                lanes[i] = null;

            var parents = commit.ParentShas;
            if (parents.Count == 0)
            {
                lanes[nodeLane] = null; // root commit: lane ends
            }
            else
            {
                lanes[nodeLane] = parents[0];
                row.Segments.Add(new GraphSegment(nodeLane, nodeLane, IsTopHalf: false, laneColors[nodeLane]));

                for (int p = 1; p < parents.Count; p++)
                {
                    string parent = parents[p];
                    int existing = -1;
                    for (int j = 0; j < lanes.Count; j++)
                    {
                        if (j != nodeLane && lanes[j] == parent)
                        {
                            existing = j;
                            break;
                        }
                    }

                    if (existing >= 0)
                    {
                        // Merge edge into a lane that already expects this parent.
                        row.Segments.Add(new GraphSegment(nodeLane, existing, IsTopHalf: false, laneColors[existing]));
                    }
                    else
                    {
                        int k = AllocateLane(lanes, laneColors, parent, nextColor++);
                        row.Segments.Add(new GraphSegment(nodeLane, k, IsTopHalf: false, laneColors[k]));
                    }
                }
            }

            TrimTrailingNulls(lanes, laneColors);

            var finalRow = new GraphRow
            {
                NodeLane = row.NodeLane,
                ColorIndex = row.ColorIndex,
                LaneCount = System.Math.Max(lanesBefore, lanes.Count)
            };
            finalRow.Segments.AddRange(row.Segments);
            rows.Add(finalRow);
        }

        return rows;
    }

    private static int AllocateLane(List<string?> lanes, List<int> colors, string sha, int color)
    {
        for (int i = 0; i < lanes.Count; i++)
        {
            if (lanes[i] is null)
            {
                lanes[i] = sha;
                colors[i] = color;
                return i;
            }
        }

        lanes.Add(sha);
        colors.Add(color);
        return lanes.Count - 1;
    }

    private static void TrimTrailingNulls(List<string?> lanes, List<int> colors)
    {
        while (lanes.Count > 0 && lanes[^1] is null)
        {
            lanes.RemoveAt(lanes.Count - 1);
            colors.RemoveAt(colors.Count - 1);
        }
    }
}
