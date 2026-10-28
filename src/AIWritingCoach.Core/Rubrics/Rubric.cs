using AIWritingCoach.Core.Common;
using AIWritingCoach.Core.Writing;

namespace AIWritingCoach.Core.Rubrics;

public enum RubricKind
{
    /// <summary>A full exam-style rubric (e.g. Cambridge-style B2 First).</summary>
    Exam,

    /// <summary>A pool of optional criteria that can be added to any check (formality, tone…).</summary>
    Custom,
}

/// <summary>A set of assessment criteria. Stored as data, seeded from JSON.</summary>
public class Rubric : Entity, IOptionallyUserOwned
{
    public Guid? UserId { get; set; }

    public string Key { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public RubricKind Kind { get; set; } = RubricKind.Exam;

    public List<RubricCriterion> Criteria { get; set; } = [];
}

public sealed record RubricCriterion
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>What the AI should assess for this criterion.</summary>
    public string Description { get; init; } = string.Empty;

    public int MinScore { get; init; }

    public int MaxScore { get; init; } = 5;
}

/// <summary>
/// A versioned prompt. Placeholders use <c>{{name}}</c> syntax and are filled by <see cref="IPromptRenderer"/>.
/// </summary>
public class PromptTemplate : Entity, IOptionallyUserOwned
{
    public const string ReviewKey = "review";

    public Guid? UserId { get; set; }

    public string Key { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public string SystemPrompt { get; set; } = string.Empty;

    public string UserTemplate { get; set; } = string.Empty;
}

public interface IPromptRenderer
{
    string Render(string template, IReadOnlyDictionary<string, string> values);
}

/// <summary>A preset ("B2 Essay", "Business email") bundling level, criteria, model and word limits.</summary>
public class Profile : Entity, IOptionallyUserOwned
{
    public Guid? UserId { get; set; }

    public string Key { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public CefrLevel TargetLevel { get; set; } = CefrLevel.B2;

    public WritingTaskType TaskType { get; set; } = WritingTaskType.Essay;

    /// <summary>Further task types this profile also fits (e.g. "Report / Proposal").</summary>
    public List<WritingTaskType> OtherTaskTypes { get; set; } = [];

    public Guid RubricId { get; set; }

    /// <summary>Criteria enabled by default; may reference criteria of the profile's rubric or of custom rubrics.</summary>
    public List<string> CriterionKeys { get; set; } = [];

    /// <summary>Preferred model; null uses the configured default chain.</summary>
    public string? Model { get; set; }

    public int? MinWords { get; set; }

    public int? MaxWords { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Task types a text written with this profile may have: the main one plus <see cref="OtherTaskTypes"/>,
    /// or every type for a general profile (task type <see cref="WritingTaskType.Other"/>).
    /// </summary>
    public IReadOnlyList<WritingTaskType> AllowedTaskTypes() => TaskType == WritingTaskType.Other
        ? Enum.GetValues<WritingTaskType>()
        : [TaskType, .. OtherTaskTypes.Where(t => t != TaskType).Distinct()];
}
