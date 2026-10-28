using AIWritingCoach.Core.Common;
using AIWritingCoach.Core.Writing;

namespace AIWritingCoach.Core.Reviews;

/// <summary>One AI check of a document at a point in time.</summary>
public class Review : Entity, IUserOwned
{
    public Guid UserId { get; set; }

    public Guid DocumentId { get; set; }

    public Guid? ProfileId { get; set; }

    public Guid? RubricId { get; set; }

    public CefrLevel TargetLevel { get; set; }

    public WritingTaskType TaskType { get; set; }

    /// <summary>The exact text sent to the AI; the diff is always computed against it.</summary>
    public string SubmittedText { get; set; } = string.Empty;

    public string CorrectedText { get; set; } = string.Empty;

    /// <summary>Result of applying the user's accept/reject decisions; null until applied.</summary>
    public string? FinalText { get; set; }

    /// <summary>Indexes of diff hunks the user rejected.</summary>
    public List<int> RejectedHunkIndexes { get; set; } = [];

    public CefrLevel? EstimatedLevel { get; set; }

    public string OverallFeedback { get; set; } = string.Empty;

    public int WordCount { get; set; }

    public int TotalScore { get; set; }

    public int MaxTotalScore { get; set; }

    public string Model { get; set; } = string.Empty;

    public string PromptTemplateKey { get; set; } = string.Empty;

    public int PromptTemplateVersion { get; set; }

    public List<ReviewError> Errors { get; set; } = [];

    public List<CriterionScore> Scores { get; set; } = [];

    public List<VocabularySuggestion> VocabularySuggestions { get; set; } = [];

    /// <summary>Overall score as a 0–100 percentage, or null when no criteria were scored.</summary>
    public double? ScorePercent => ScoreMath.Percent(TotalScore, MaxTotalScore);
}

public static class ScoreMath
{
    public static double? Percent(int total, int max) =>
        max > 0 ? Math.Round(100.0 * total / max, 1) : null;
}

public enum ErrorSeverity
{
    Minor,
    Moderate,
    Major,
}

/// <summary>A single mistake reported by the AI.</summary>
public class ReviewError : Entity
{
    public Guid ReviewId { get; set; }

    /// <summary>Position in the AI's list; used to link diff hunks back to errors.</summary>
    public int Index { get; set; }

    public string Original { get; set; } = string.Empty;

    public string Correction { get; set; } = string.Empty;

    /// <summary>E.g. Grammar, Spelling, Punctuation, Vocabulary, Style, Register, Cohesion.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>E.g. "Subject-verb agreement", "Article", "Collocation".</summary>
    public string? Subcategory { get; set; }

    public string Explanation { get; set; } = string.Empty;

    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Moderate;
}

/// <summary>Score for one rubric criterion. Name and range are snapshotted so history survives rubric edits.</summary>
public class CriterionScore : Entity
{
    public Guid ReviewId { get; set; }

    public string CriterionKey { get; set; } = string.Empty;

    public string CriterionName { get; set; } = string.Empty;

    public int Score { get; set; }

    public int MaxScore { get; set; }

    public string? Comment { get; set; }
}

/// <summary>"Instead of X use Y" suggestion from a review.</summary>
public class VocabularySuggestion : Entity
{
    public Guid ReviewId { get; set; }

    public int Index { get; set; }

    public string Original { get; set; } = string.Empty;

    public string Suggestion { get; set; } = string.Empty;

    public CefrLevel? CefrLevel { get; set; }

    public string? Example { get; set; }
}
