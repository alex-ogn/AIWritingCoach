using System.Text.RegularExpressions;
using AIWritingCoach.Core.Diff;
using AIWritingCoach.Core.Reviews;
using DiffPlex;
using DiffPlex.Model;

namespace AIWritingCoach.Infrastructure.Diff;

/// <summary>
/// Word-level diff computed locally with DiffPlex (the AI's own change list is never trusted for
/// positions). Hunks are then linked to AI errors by locating each error's text in the source.
/// </summary>
public sealed partial class DiffPlexTextDiffService : ITextDiffService
{
    private static readonly Differ Differ = new();

    public DiffDocument Compute(string originalText, string correctedText, IReadOnlyList<ReviewError> errors)
    {
        var result = Differ.CreateDiffs(originalText, correctedText, ignoreWhiteSpace: false, ignoreCase: false, new TokenChunker());
        var oldPieces = result.PiecesOld;
        var newPieces = result.PiecesNew;
        var oldOffsets = Offsets(oldPieces);
        var newOffsets = Offsets(newPieces);

        var segments = new List<DiffSegment>();
        int posA = 0, posB = 0, hunkIndex = 0;

        foreach (var block in MergeAcrossWhitespace(result.DiffBlocks, oldPieces))
        {
            if (block.DeleteStartA > posA)
            {
                segments.Add(new DiffSegment(Join(oldPieces, posA, block.DeleteStartA - posA), null));
            }

            var hunk = new DiffHunk
            {
                Index = hunkIndex++,
                OriginalStart = oldOffsets[block.DeleteStartA],
                OriginalText = Join(oldPieces, block.DeleteStartA, block.DeleteCountA),
                CorrectedStart = newOffsets[block.InsertStartB],
                CorrectedText = Join(newPieces, block.InsertStartB, block.InsertCountB),
            };
            segments.Add(new DiffSegment(string.Empty, hunk));

            posA = block.DeleteStartA + block.DeleteCountA;
            posB = block.InsertStartB + block.InsertCountB;
        }

        if (posA < oldPieces.Count)
        {
            segments.Add(new DiffSegment(Join(oldPieces, posA, oldPieces.Count - posA), null));
        }

        var diff = new DiffDocument(originalText, correctedText, segments);
        LinkErrors(diff, errors);
        return diff;
    }

    /// <summary>"more better" → "better": treat changes separated by a single space as one hunk.</summary>
    internal static IEnumerable<DiffBlock> MergeAcrossWhitespace(IList<DiffBlock> blocks, IReadOnlyList<string> oldPieces)
    {
        DiffBlock? current = null;
        foreach (var block in blocks)
        {
            if (current is null)
            {
                current = block;
                continue;
            }

            var gapStart = current.DeleteStartA + current.DeleteCountA;
            var gap = block.DeleteStartA - gapStart;
            if (gap == 1 && string.IsNullOrWhiteSpace(oldPieces[gapStart]) && !oldPieces[gapStart].Contains('\n'))
            {
                current = new DiffBlock(
                    current.DeleteStartA,
                    block.DeleteStartA + block.DeleteCountA - current.DeleteStartA,
                    current.InsertStartB,
                    block.InsertStartB + block.InsertCountB - current.InsertStartB);
            }
            else
            {
                yield return current;
                current = block;
            }
        }

        if (current is not null)
        {
            yield return current;
        }
    }

    internal static void LinkErrors(DiffDocument diff, IReadOnlyList<ReviewError> errors)
    {
        foreach (var error in errors)
        {
            var linked = TryLink(diff, error, FindAll(diff.OriginalText, error.Original), useOriginal: true)
                || TryLink(diff, error, FindAll(diff.CorrectedText, error.Correction), useOriginal: false);
            _ = linked;
        }
    }

    private static bool TryLink(DiffDocument diff, ReviewError error, IReadOnlyList<(int Start, int End)> ranges, bool useOriginal)
    {
        List<DiffHunk>? fallback = null;
        foreach (var (start, end) in ranges)
        {
            var candidates = diff.Hunks
                .Where(h => useOriginal
                    ? Overlaps(h.OriginalStart, h.OriginalEnd, start, end)
                    : Overlaps(h.CorrectedStart, h.CorrectedEnd, start, end))
                .ToList();
            if (candidates.Count == 0)
            {
                continue;
            }

            // Prefer the occurrence whose change is not explained yet (repeated words like "the").
            if (candidates.Any(h => h.ErrorIndexes.Count == 0))
            {
                Link(candidates, error.Index);
                return true;
            }

            fallback ??= candidates;
        }

        if (fallback is not null)
        {
            Link(fallback, error.Index);
            return true;
        }

        return false;
    }

    private static void Link(IEnumerable<DiffHunk> hunks, int errorIndex)
    {
        foreach (var hunk in hunks.Where(h => !h.ErrorIndexes.Contains(errorIndex)))
        {
            hunk.ErrorIndexes.Add(errorIndex);
        }
    }

    internal static bool Overlaps(int hunkStart, int hunkEnd, int start, int end) =>
        hunkStart == hunkEnd
            ? start <= hunkStart && hunkStart <= end
            : hunkStart < end && start < hunkEnd;

    private static List<(int Start, int End)> FindAll(string text, string? fragment)
    {
        var ranges = new List<(int, int)>();
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return ranges;
        }

        foreach (var comparison in new[] { StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase })
        {
            for (var i = text.IndexOf(fragment, comparison); i >= 0; i = text.IndexOf(fragment, i + 1, comparison))
            {
                ranges.Add((i, i + fragment.Length));
            }

            if (ranges.Count > 0)
            {
                break;
            }
        }

        return ranges;
    }

    private static int[] Offsets(IReadOnlyList<string> pieces)
    {
        var offsets = new int[pieces.Count + 1];
        for (var i = 0; i < pieces.Count; i++)
        {
            offsets[i + 1] = offsets[i] + pieces[i].Length;
        }

        return offsets;
    }

    private static string Join(IReadOnlyList<string> pieces, int start, int count) =>
        count <= 0 ? string.Empty : string.Concat(pieces.Skip(start).Take(count));

    // Words (with inner apostrophes/hyphens), whitespace runs, or single other characters.
    // Every character belongs to exactly one token, so tokens concatenate back to the input.
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*|\s+|.", RegexOptions.Singleline)]
    private static partial Regex TokenRegex();

    internal static string[] Tokenize(string text) =>
        TokenRegex().Matches(text).Select(m => m.Value).ToArray();

    private sealed class TokenChunker : IChunker
    {
        public IReadOnlyList<string> Chunk(string text) => Tokenize(text);
    }
}
