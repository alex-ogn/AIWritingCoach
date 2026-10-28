using AIWritingCoach.Core.Common;

namespace AIWritingCoach.Core.Usage;

public enum AiCallStatus
{
    Success,
    RateLimited,
    Failed,
}

/// <summary>One LLM request attempt; kept for cost analysis and future pricing/limits.</summary>
public class AiUsageLog : Entity, IUserOwned
{
    public Guid UserId { get; set; }

    public string Feature { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public long? InputTokens { get; set; }

    public long? OutputTokens { get; set; }

    /// <summary>Estimated cost in USD; null when pricing is unknown.</summary>
    public decimal? Cost { get; set; }

    public int LatencyMs { get; set; }

    public AiCallStatus Status { get; set; }

    public string? Error { get; set; }
}

public interface IUsageLogger
{
    Task LogAsync(AiUsageLog entry, CancellationToken cancellationToken = default);
}
