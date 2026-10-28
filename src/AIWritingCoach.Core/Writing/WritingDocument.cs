using AIWritingCoach.Core.Common;

namespace AIWritingCoach.Core.Writing;

public enum WritingTaskType
{
    Email,
    Essay,
    Letter,
    Report,
    Review,
    Article,
    Proposal,
    Story,
    Other,
}

public enum CefrLevel
{
    A1,
    A2,
    B1,
    B2,
    C1,
    C2,
}

public static class CefrLevelParser
{
    public static CefrLevel? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Accept "B2", "b2", "B2+" or "B2 (upper intermediate)".
        var head = value.Trim().ToUpperInvariant();
        head = head.Length >= 2 ? head[..2] : head;
        return Enum.TryParse<CefrLevel>(head, out var level) && Enum.IsDefined(level) ? level : null;
    }
}

/// <summary>A text the user writes and returns to over time.</summary>
public class WritingDocument : Entity, IUserOwned
{
    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public WritingTaskType TaskType { get; set; } = WritingTaskType.Essay;

    /// <summary>The exam task / instructions the text answers (optional).</summary>
    public string? TaskPrompt { get; set; }

    public string Content { get; set; } = string.Empty;

    public int WordCount { get; set; }

    public CefrLevel TargetLevel { get; set; } = CefrLevel.B2;

    public Guid? ProfileId { get; set; }

    public Guid? LastReviewId { get; set; }
}

/// <summary>Lightweight row for the History list.</summary>
public sealed record DocumentSummary(
    Guid Id,
    string Title,
    WritingTaskType TaskType,
    CefrLevel TargetLevel,
    int WordCount,
    DateTime UpdatedAt,
    double? LastScorePercent,
    CefrLevel? LastEstimatedLevel,
    int ReviewCount);
