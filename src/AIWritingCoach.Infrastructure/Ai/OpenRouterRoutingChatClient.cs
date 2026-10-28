using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace AIWritingCoach.Infrastructure.Ai;

/// <summary>
/// Innermost client: the OpenAI SDK pointed at OpenRouter. Routes each call to <see cref="ChatOptions.ModelId"/>
/// (an OpenAI <c>ChatClient</c> is bound to one model). The SDK's own retries are disabled —
/// <see cref="ModelFallbackChatClient"/> owns retry/fallback.
/// </summary>
public sealed class OpenRouterRoutingChatClient(IOptions<OpenRouterOptions> options) : IChatClient
{
    private readonly OpenRouterOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, IChatClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<OpenAIClient> _openAi = new(() => CreateClient(options.Value));

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        ClientFor(options).GetResponseAsync(messages, options, cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        ClientFor(options).GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    // Singleton shared by scoped pipelines; their disposal must not tear it down.
    public void Dispose()
    {
    }

    private IChatClient ClientFor(ChatOptions? options)
    {
        var model = string.IsNullOrWhiteSpace(options?.ModelId) ? _options.DefaultModel : options.ModelId;
        return _clients.GetOrAdd(model, m => _openAi.Value.GetChatClient(m).AsIChatClient());
    }

    private static OpenAIClient CreateClient(OpenRouterOptions options)
    {
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException("OpenRouter API key is not configured (OpenRouter:ApiKey).");
        }

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.BaseUrl),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            NetworkTimeout = options.Timeout,
        };
        clientOptions.AddPolicy(new OpenRouterHeadersPolicy(options.AppUrl, options.AppName), PipelinePosition.PerCall);
        return new OpenAIClient(new ApiKeyCredential(options.ApiKey!), clientOptions);
    }

    private sealed class OpenRouterHeadersPolicy(string? appUrl, string? appName) : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddHeaders(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddHeaders(message);
            return ProcessNextAsync(message, pipeline, currentIndex);
        }

        private void AddHeaders(PipelineMessage message)
        {
            if (!string.IsNullOrWhiteSpace(appUrl))
            {
                message.Request.Headers.Set("HTTP-Referer", appUrl);
            }

            if (!string.IsNullOrWhiteSpace(appName))
            {
                message.Request.Headers.Set("X-Title", appName);
            }
        }
    }
}
