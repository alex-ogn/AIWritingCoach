namespace AIWritingCoach.Core.Diff;

public sealed record SidePiece(string Text, DiffHunk? Hunk);

/// <summary>One aligned row (paragraph/line) of a side-by-side diff.</summary>
public sealed class SideBySideRow
{
    public List<SidePiece> Left { get; } = [];

    public List<SidePiece> Right { get; } = [];

    public bool HasChanges => Left.Any(p => p.Hunk is not null) || Right.Any(p => p.Hunk is not null);
}

/// <summary>
/// Splits a diff into rows aligned on line breaks of unchanged text, like WinMerge.
/// Line breaks inside a change stay inside the row.
/// </summary>
public static class SideBySideLayout
{
    public static IReadOnlyList<SideBySideRow> Build(DiffDocument diff)
    {
        var rows = new List<SideBySideRow>();
        var current = new SideBySideRow();

        foreach (var segment in diff.Segments)
        {
            if (segment.Hunk is { } hunk)
            {
                if (hunk.OriginalText.Length > 0)
                {
                    current.Left.Add(new SidePiece(hunk.OriginalText, hunk));
                }

                if (hunk.CorrectedText.Length > 0)
                {
                    current.Right.Add(new SidePiece(hunk.CorrectedText, hunk));
                }

                // Keep a zero-width anchor so the hunk is clickable on both sides.
                if (hunk.OriginalText.Length == 0)
                {
                    current.Left.Add(new SidePiece(string.Empty, hunk));
                }

                if (hunk.CorrectedText.Length == 0)
                {
                    current.Right.Add(new SidePiece(string.Empty, hunk));
                }

                continue;
            }

            var lines = segment.Text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    rows.Add(current);
                    current = new SideBySideRow();
                }

                var line = lines[i].TrimEnd('\r');
                if (line.Length > 0)
                {
                    current.Left.Add(new SidePiece(line, null));
                    current.Right.Add(new SidePiece(line, null));
                }
            }
        }

        rows.Add(current);
        return rows;
    }
}
