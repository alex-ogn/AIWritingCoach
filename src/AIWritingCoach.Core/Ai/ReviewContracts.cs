using System.ComponentModel;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Writing;

namespace AIWritingCoach.Core.Ai;

/// <summary>Everything the AI needs to check one text.</summary>
public sealed record ReviewRequest
{
    public required string Text { get; init; }

    public WritingTaskType TaskType { get; init; } = WritingTaskType.Essay;

    public CefrLevel TargetLevel { get; init; } = CefrLevel.B2;

    public string? TaskPrompt { get; init; }

    public IReadOnlyList<RubricCriterion> Criteria { get; init; } = [];

    public int? MinWords { get; init; }

    public int? MaxWords { get; init; }

    /// <summary>Preferred model; null uses the configured default and fallback chain.</summary>
    public string? Model { get; init; }

    /// <summary>Language used for explanations and feedback.</summary>
    public string FeedbackLanguage { get; init; } = "English";
}

/// <summary>What the AI needs to suggest a title for a text that has not been written yet.</summary>
public sealed record TitleRequest
{
    public string? ProfileName { get; init; }

    public WritingTaskType TaskType { get; init; } = WritingTaskType.Essay;

    public CefrLevel TargetLevel { get; init; } = CefrLevel.B2;

    public string? TaskPrompt { get; init; }

    /// <summary>Preferred model; null uses the configured default and fallback chain.</summary>
    public string? Model { get; init; }
}

public sealed record AiReviewResponse(
    ReviewResult Result,
    string Model,
    string PromptTemplateKey,
    int PromptTemplateVersion,
    int Attempts);

public interface IAiReviewService
{
    /// <exception cref="AiReviewException">The provider failed or returned no valid result.</exception>
    Task<AiReviewResponse> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Suggests a random title that fits the task type, level and (optional) task instructions.</summary>
    /// <exception cref="AiReviewException">The provider failed or returned no usable title.</exception>
    Task<string> SuggestTitleAsync(TitleRequest request, CancellationToken cancellationToken = default);
}

public enum AiReviewFailure
{
    NotConfigured,
    RateLimited,
    InvalidResponse,
    ProviderError,
}

public sealed class AiReviewException(AiReviewFailure reason, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public AiReviewFailure Reason { get; } = reason;
}

// ---- Structured output returned by the model (JSON, camelCase). ----

public sealed class ReviewResult
{
    [Description("The full text with all corrections applied. Keep the author's paragraphs, line breaks and meaning; change only what is needed.")]
    public string CorrectedText { get; set; } = string.Empty;

    [Description("Estimated CEFR level of the ORIGINAL text: A1, A2, B1, B2, C1 or C2.")]
    public string EstimatedLevel { get; set; } = string.Empty;

    [Description("2-5 sentences of overall feedback: strengths first, then the most important things to improve.")]
    public string OverallFeedback { get; set; } = string.Empty;

    [Description("Every change made in correctedText, in the order it appears in the text.")]
    public List<ReviewResultError> Errors { get; set; } = [];

    [Description("Optional upgrades: more precise or higher-level words/phrases the author could use.")]
    public List<ReviewResultVocabulary> VocabularySuggestions { get; set; } = [];

    [Description("One score per requested criterion.")]
    public List<ReviewResultScore> Scores { get; set; } = [];
}

public sealed class ReviewResultError
{
    [Description("Exact substring of the original text that is wrong (empty only for pure insertions).")]
    public string Original { get; set; } = string.Empty;

    [Description("The replacement used in correctedText (empty for pure deletions).")]
    public string Correction { get; set; } = string.Empty;

    [Description("One of: Grammar, Spelling, Punctuation, Vocabulary, WordChoice, Style, Register, Cohesion, Capitalisation, Other.")]
    public string Category { get; set; } = string.Empty;

    [Description("Finer label, e.g. 'Subject-verb agreement', 'Articles', 'Collocation', 'Tense'.")]
    public string? Subcategory { get; set; }

    [Description("Short, learner-friendly explanation of the rule or reason.")]
    public string Explanation { get; set; } = string.Empty;

    [Description("minor, moderate or major.")]
    public string Severity { get; set; } = "moderate";
}

public sealed class ReviewResultVocabulary
{
    [Description("The word or phrase the author used.")]
    public string Original { get; set; } = string.Empty;

    [Description("A better alternative.")]
    public string Suggestion { get; set; } = string.Empty;

    [Description("CEFR level of the suggested word: A1-C2.")]
    public string? CefrLevel { get; set; }

    [Description("Short example sentence using the suggestion.")]
    public string? Example { get; set; }
}

public sealed class ReviewResultScore
{
    [Description("The criterion key exactly as given in the request.")]
    public string Criterion { get; set; } = string.Empty;

    [Description("Integer score within the criterion's range.")]
    public int Score { get; set; }

    [Description("One or two sentences justifying the score.")]
    public string? Comment { get; set; }
}
