using AIWritingCoach.Core.Common;
using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Writing;

namespace AIWritingCoach.Core.Vocabulary;

public enum VocabularyStatus
{
    New,
    Learned,
}

/// <summary>How a word got into the list.</summary>
public enum VocabularySource
{
    Manual,

    /// <summary>Added automatically from a Spelling mistake in a review.</summary>
    Spelling,

    /// <summary>Added by the user from a review's vocabulary upgrades.</summary>
    Upgrade,
}

/// <summary>A word or phrase the user wants to start using.</summary>
public class VocabularyItem : Entity, IUserOwned
{
    public Guid UserId { get; set; }

    public string Word { get; set; } = string.Empty;

    /// <summary>The weaker expression (or misspelling) it replaces, if any.</summary>
    public string? Replaces { get; set; }

    public CefrLevel? CefrLevel { get; set; }

    public string? Example { get; set; }

    public VocabularyStatus Status { get; set; } = VocabularyStatus.New;

    public VocabularySource Source { get; set; } = VocabularySource.Manual;

    public Guid? SourceReviewId { get; set; }
}

public static class VocabularyFactory
{
    public const int MaxWordLength = 200;

    /// <summary>One item per distinct misspelled word of the review: the correct spelling, replacing the wrong one.</summary>
    public static IReadOnlyList<VocabularyItem> FromSpellingErrors(Review review) =>
        review.Errors
            .Where(e => string.Equals(e.Category, "Spelling", StringComparison.OrdinalIgnoreCase))
            .Select(e => (Wrong: e.Original.Trim(), Right: e.Correction.Trim()))
            .Where(x => x.Wrong.Length is > 0 and <= MaxWordLength
                && x.Right.Length is > 0 and <= MaxWordLength
                && !string.Equals(x.Wrong, x.Right, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(x => x.Right, StringComparer.OrdinalIgnoreCase)
            .Select(x => new VocabularyItem
            {
                UserId = review.UserId,
                Word = x.Right,
                Replaces = x.Wrong,
                Source = VocabularySource.Spelling,
                SourceReviewId = review.Id,
            })
            .ToList();

    public static VocabularyItem FromSuggestion(Review review, VocabularySuggestion suggestion) => new()
    {
        UserId = review.UserId,
        Word = suggestion.Suggestion.Trim(),
        Replaces = string.IsNullOrWhiteSpace(suggestion.Original) ? null : suggestion.Original.Trim(),
        CefrLevel = suggestion.CefrLevel,
        Example = suggestion.Example,
        Source = VocabularySource.Upgrade,
        SourceReviewId = review.Id,
    };
}
