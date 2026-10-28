using System.ClientModel;
using System.Diagnostics;
using AIWritingCoach.Core.Usage;
using Microsoft.Extensions.AI;

namespace AIWritingCoach.Infrastructure.Ai;

/// <summary>Ambient name of the feature making AI calls, recorded in <see cref="AiUsageLog.Feature"/>.</summary>
public static class AiFeature
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string Name => Current.Value ?? "unknown";

    public static IDisposable Begin(string feature)
    {
        var previous = Current.Value;
        Current.Value = feature;
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}

/// <summary>Logs tokens, estimated cost and latency of every attempt (successful or not).</summary>
public sealed class UsageLoggingChatClient(IChatClient innerClient, IUsageLogger usageLogger, IModelPricing pricing)
    : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var requestedModel = options?.ModelId ?? "default";
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            var model = response.ModelId ?? requestedModel;
            var input = response.Usage?.InputTokenCount;
            var output = response.Usage?.OutputTokenCount;

            await usageLogger.LogAsync(
                new AiUsageLog
                {
                    Feature = AiFeature.Name,
                    Model = model,
                    InputTokens = input,
                    OutputTokens = output,
                    Cost = await pricing.EstimateCostAsync(requestedModel, input, output, cancellationToken),
                    LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                    Status = AiCallStatus.Success,
                },
                CancellationToken.None);
            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await usageLogger.LogAsync(
                new AiUsageLog
                {
                    Feature = AiFeature.Name,
                    Model = requestedModel,
                    LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                    Status = ex is ClientResultException { Status: 429 } ? AiCallStatus.RateLimited : AiCallStatus.Failed,
                    Error = Truncate(ex.Message, 2000),
                },
                CancellationToken.None);
            throw;
        }
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
