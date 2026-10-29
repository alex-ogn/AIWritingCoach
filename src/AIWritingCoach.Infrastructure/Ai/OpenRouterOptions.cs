namespace AIWritingCoach.Infrastructure.Ai;

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    /// <summary>Secret. Set via user-secrets (development) or the <c>OpenRouter__ApiKey</c> environment variable.</summary>
    public string? ApiKey { get; set; }

    public string DefaultModel { get; set; } = "openai/gpt-4o-mini";

    /// <summary>Tried in order when the requested model is rate-limited or unavailable (free → paid).</summary>
    public List<string> FallbackModels { get; set; } = [];

    /// <summary>Retries of the same model on 429/5xx before moving to the next one.</summary>
    public int MaxRetriesPerModel { get; set; } = 2;

    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Use JSON-schema structured output; models without support fall back to plain JSON mode.</summary>
    public bool UseJsonSchema { get; set; } = true;

    /// <summary>Total attempts when the model returns invalid JSON (first try + repair retries).</summary>
    public int MaxParseAttempts { get; set; } = 2;

    public int MaxOutputTokens { get; set; } = 8000;

    public float Temperature { get; set; } = 0.2f;

    /// <summary>Sent as HTTP-Referer / X-Title for OpenRouter app attribution.</summary>
    public string? AppUrl { get; set; } = "https://github.com/";

    public string AppName { get; set; } = "AIWritingCoach";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public IReadOnlyList<string> ModelChain(string? requested) =>
        new[] { string.IsNullOrWhiteSpace(requested) ? DefaultModel : requested }
            .Concat(FallbackModels)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
