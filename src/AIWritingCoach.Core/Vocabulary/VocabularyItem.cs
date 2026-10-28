using AIWritingCoach.Core.Common;
using AIWritingCoach.Core.Writing;

namespace AIWritingCoach.Core.Vocabulary;

public enum VocabularyStatus
{
    New,
    Learned,
}

/// <summary>A word or phrase the user wants to start using.</summary>
public class VocabularyItem : Entity, IUserOwned
{
    public Guid UserId { get; set; }

    public string Word { get; set; } = string.Empty;

    /// <summary>The weaker expression it replaces, if any.</summary>
    public string? Replaces { get; set; }

    public CefrLevel? CefrLevel { get; set; }

    public string? Example { get; set; }

    public VocabularyStatus Status { get; set; } = VocabularyStatus.New;

    public Guid? SourceReviewId { get; set; }
}
