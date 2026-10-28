using System.ClientModel;
using System.Globalization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AIWritingCoach.Infrastructure.Ai;

/// <summary>Thrown when every model in the chain failed.</summary>
public sealed class AiProviderUnavailableException(bool rateLimited, string message, Exception? inner)
    : Exception(message, inner)
{
    public bool RateLimited { get; } = rateLimited;
}

/// <summary>
/// Retries 429/5xx with exponential backoff (honouring <c>Retry-After</c>) and then falls back through
/// <see cref="OpenRouterOptions.FallbackModels"/>. A free-model daily quota skips straight to the next model.
/// </summary>
public sealed class ModelFallbackChatClient(
    IChatClient innerClient,
    OpenRouterOptions options,
    TimeProvider timeProvider,
    ILogger<ModelFallbackChatClient> logger) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? chatOptions = null, CancellationToken cancellationToken = default)
    {
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        Exception? lastError = null;
        var anyRateLimited = false;

        foreach (var model in options.ModelChain(chatOptions?.ModelId))
        {
            var attemptOptions = chatOptions?.Clone() ?? new ChatOptions();
            attemptOptions.ModelId = model;

            for (var retry = 0; ; retry++)
            {
                try
                {
                    var response = await base.GetResponseAsync(messageList, attemptOptions, cancellationToken);
                    response.ModelId ??= model;
                    return response;
                }
                catch (Exception ex) when (Classify(ex) is var kind && kind != FailureKind.Fatal && !cancellationToken.IsCancellationRequested)
                {
                    lastError = ex;
                    anyRateLimited |= kind is FailureKind.RateLimited or FailureKind.QuotaExhausted;

                    if (kind is FailureKind.NextModel or FailureKind.QuotaExhausted || retry >= options.MaxRetriesPerModel)
                    {
                        logger.LogWarning("Model {Model} failed ({Kind}): {Message}. Trying next model.", model, kind, ex.Message);
                        break;
                    }

                    var delay = RetryDelay(ex, retry);
                    logger.LogInformation("Model {Model} failed ({Kind}); retry {Retry} in {Delay}.", model, kind, retry + 1, delay);
                    await Task.Delay(delay, timeProvider, cancellationToken);
                }
            }
        }

        throw new AiProviderUnavailableException(
            anyRateLimited,
            anyRateLimited ? "All models are rate-limited. Try again later or add a paid fallback model." : "All models failed.",
            lastError);
    }

    internal enum FailureKind
    {
        /// <summary>Do not retry or fall back (bad request, auth).</summary>
        Fatal,

        /// <summary>Retry the same model with backoff (429, 5xx, timeouts).</summary>
        RateLimited,

        Transient,

        /// <summary>Daily quota of free models used up: go straight to the next model.</summary>
        QuotaExhausted,

        /// <summary>Model missing or no credits for it: go straight to the next model.</summary>
        NextModel,
    }

    internal static FailureKind Classify(Exception ex) => ex switch
    {
        ClientResultException { Status: 429 } c when IsDailyQuota(c) => FailureKind.QuotaExhausted,
        ClientResultException { Status: 429 } => FailureKind.RateLimited,
        ClientResultException { Status: 402 or 404 } => FailureKind.NextModel,
        ClientResultException { Status: 408 or >= 500 } => FailureKind.Transient,
        ClientResultException { Status: 0 } => FailureKind.Transient,
        HttpRequestException or TimeoutException or TaskCanceledException => FailureKind.Transient,
        _ => FailureKind.Fatal,
    };

    private static bool IsDailyQuota(ClientResultException ex) =>
        ex.Message.Contains("per-day", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("per day", StringComparison.OrdinalIgnoreCase);

    private TimeSpan RetryDelay(Exception ex, int retry)
    {
        if (ex is ClientResultException c && c.GetRawResponse() is { } raw
            && raw.Headers.TryGetValue("Retry-After", out var header)
            && double.TryParse(header, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(Math.Clamp(seconds, 0, options.MaxRetryDelay.TotalSeconds));
        }

        var exponential = options.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, retry);
        var jitter = Random.Shared.NextDouble() * 0.25 * exponential;
        return TimeSpan.FromMilliseconds(Math.Min(exponential + jitter, options.MaxRetryDelay.TotalMilliseconds));
    }
}
