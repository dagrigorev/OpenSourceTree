using System;
using System.Collections.Generic;
using OpenSourceTree.Models;

namespace OpenSourceTree.Services;

public static class DiffParser
{
    /// <summary>Parses unified diff text (a LibGit2Sharp Patch content) into displayable lines.</summary>
    public static List<DiffLine> Parse(string patchText)
    {
        var lines = new List<DiffLine>();
        if (string.IsNullOrEmpty(patchText))
            return lines;

        int oldNo = 0, newNo = 0;
        foreach (var raw in patchText.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            if (line.StartsWith("diff --git", StringComparison.Ordinal) ||
                line.StartsWith("index ", StringComparison.Ordinal) ||
                line.StartsWith("--- ", StringComparison.Ordinal) ||
                line.StartsWith("+++ ", StringComparison.Ordinal) ||
                line.StartsWith("new file", StringComparison.Ordinal) ||
                line.StartsWith("deleted file", StringComparison.Ordinal) ||
                line.StartsWith("old mode", StringComparison.Ordinal) ||
                line.StartsWith("new mode", StringComparison.Ordinal) ||
                line.StartsWith("similarity", StringComparison.Ordinal) ||
                line.StartsWith("rename ", StringComparison.Ordinal) ||
                line.StartsWith("Binary files", StringComparison.Ordinal) ||
                line.StartsWith(@"\ No newline", StringComparison.Ordinal))
            {
                lines.Add(new DiffLine(DiffLineKind.FileHeader, line, null, null));
                continue;
            }

            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                // @@ -oldStart,oldCount +newStart,newCount @@ context
                lines.Add(new DiffLine(DiffLineKind.HunkHeader, line, null, null));
                try
                {
                    var parts = line.Split(' ');
                    var oldPart = parts[1].TrimStart('-').Split(',');
                    var newPart = parts[2].TrimStart('+').Split(',');
                    oldNo = int.Parse(oldPart[0]);
                    newNo = int.Parse(newPart[0]);
                }
                catch
                {
                    oldNo = newNo = 0;
                }
                continue;
            }

            if (line.StartsWith('+'))
            {
                lines.Add(new DiffLine(DiffLineKind.Added, line.Length > 1 ? line[1..] : "", null, newNo));
                newNo++;
            }
            else if (line.StartsWith('-'))
            {
                lines.Add(new DiffLine(DiffLineKind.Removed, line.Length > 1 ? line[1..] : "", oldNo, null));
                oldNo++;
            }
            else
            {
                if (line.Length == 0 && lines.Count == 0)
                    continue;
                lines.Add(new DiffLine(DiffLineKind.Context, line.Length > 1 ? line[1..] : "", oldNo, newNo));
                oldNo++;
                newNo++;
            }
        }

        // Drop a single trailing empty context line produced by the final newline split.
        if (lines.Count > 0 && lines[^1].Kind == DiffLineKind.Context && lines[^1].Text.Length == 0)
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }
}
