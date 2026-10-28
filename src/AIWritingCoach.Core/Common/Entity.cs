namespace AIWritingCoach.Core.Common;

/// <summary>
/// Base type for all persisted entities. GUID v7 keys keep inserts index-friendly
/// and allow ids to be generated offline (future sync).
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>UTC creation time; set by the persistence layer.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC time of the last modification; set by the persistence layer.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>An entity that always belongs to exactly one user.</summary>
public interface IUserOwned
{
    Guid UserId { get; set; }
}

/// <summary>An entity that is either system-wide (<c>UserId == null</c>) or owned by one user.</summary>
public interface IOptionallyUserOwned
{
    Guid? UserId { get; set; }
}
