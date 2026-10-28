using System.Text;
using AIWritingCoach.Core.Reviews;

namespace AIWritingCoach.Core.Diff;

public enum DiffHunkKind
{
    Insert,
    Delete,
    Replace,
}

/// <summary>One contiguous change between the submitted and the corrected text.</summary>
public sealed class DiffHunk
{
    public required int Index { get; init; }

    public required int OriginalStart { get; init; }

    public required string OriginalText { get; init; }

    public required int CorrectedStart { get; init; }

    public required string CorrectedText { get; init; }

    /// <summary><see cref="ReviewError.Index"/> values of the AI errors that explain this change.</summary>
    public List<int> ErrorIndexes { get; } = [];

    public bool Accepted { get; set; } = true;

    public int OriginalEnd => OriginalStart + OriginalText.Length;

    public int CorrectedEnd => CorrectedStart + CorrectedText.Length;

    public DiffHunkKind Kind => OriginalText.Length == 0
        ? DiffHunkKind.Insert
        : CorrectedText.Length == 0 ? DiffHunkKind.Delete : DiffHunkKind.Replace;
}

/// <summary>Either unchanged text (<see cref="Hunk"/> is null) or a change.</summary>
public sealed record DiffSegment(string Text, DiffHunk? Hunk)
{
    public bool IsChange => Hunk is not null;
}

/// <summary>
/// Locally computed diff between the submitted and the AI-corrected text, with per-hunk
/// accept/reject state.
/// </summary>
public sealed class DiffDocument(string originalText, string correctedText, IReadOnlyList<DiffSegment> segments)
{
    public string OriginalText { get; } = originalText;

    public string CorrectedText { get; } = correctedText;

    public IReadOnlyList<DiffSegment> Segments { get; } = segments;

    public IReadOnlyList<DiffHunk> Hunks { get; } = segments.Where(s => s.Hunk is not null).Select(s => s.Hunk!).ToList();

    public IReadOnlyList<int> RejectedIndexes => Hunks.Where(h => !h.Accepted).Select(h => h.Index).ToList();

    public void SetAll(bool accepted)
    {
        foreach (var hunk in Hunks)
        {
            hunk.Accepted = accepted;
        }
    }

    public void ApplyRejections(IEnumerable<int> rejectedIndexes)
    {
        var rejected = rejectedIndexes.ToHashSet();
        foreach (var hunk in Hunks)
        {
            hunk.Accepted = !rejected.Contains(hunk.Index);
        }
    }

    public IEnumerable<DiffHunk> HunksForError(int errorIndex) => Hunks.Where(h => h.ErrorIndexes.Contains(errorIndex));

    /// <summary>Builds the final text: corrected text for accepted hunks, original text for rejected ones.</summary>
    public string BuildText()
    {
        var builder = new StringBuilder(Math.Max(OriginalText.Length, CorrectedText.Length));
        foreach (var segment in Segments)
        {
            builder.Append(segment.Hunk switch
            {
                null => segment.Text,
                { Accepted: true } hunk => hunk.CorrectedText,
                var hunk => hunk.OriginalText,
            });
        }

        return builder.ToString();
    }
}

public interface ITextDiffService
{
    /// <summary>Computes the diff locally and links hunks to the AI's errors.</summary>
    DiffDocument Compute(string originalText, string correctedText, IReadOnlyList<ReviewError> errors);
}
