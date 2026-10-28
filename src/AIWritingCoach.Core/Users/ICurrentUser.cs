namespace AIWritingCoach.Core.Users;

public enum AppMode
{
    /// <summary>No login, one fixed user, bound to localhost only.</summary>
    Personal,

    /// <summary>ASP.NET Core Identity / OIDC, usage limits, payments.</summary>
    MultiUser,
}

/// <summary>
/// The user on whose behalf the current operation runs. All user-owned data access
/// goes through this abstraction so Personal and MultiUser modes share the same code.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    bool IsAuthenticated { get; }
}
