using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIWritingCoach.Infrastructure.Ai;

public interface IModelPricing
{
    /// <summary>Estimated USD cost of a call, or null when the model's price is unknown.</summary>
    Task<decimal?> EstimateCostAsync(string model, long? inputTokens, long? outputTokens, CancellationToken cancellationToken = default);
}

/// <summary>Prices from OpenRouter's public <c>/models</c> endpoint, cached in memory.</summary>
public sealed class OpenRouterPricing(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenRouterOptions> options,
    TimeProvider timeProvider,
    ILogger<OpenRouterPricing> logger) : IModelPricing
{
    public const string HttpClientName = "openrouter-pricing";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, (decimal Prompt, decimal Completion)> _prices = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;

    public async Task<decimal?> EstimateCostAsync(string model, long? inputTokens, long? outputTokens, CancellationToken cancellationToken = default)
    {
        if (model.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        if (inputTokens is null && outputTokens is null)
        {
            return null;
        }

        var prices = await GetPricesAsync(cancellationToken);
        return prices.TryGetValue(model, out var price)
            ? (inputTokens ?? 0) * price.Prompt + (outputTokens ?? 0) * price.Completion
            : null;
    }

    private async Task<Dictionary<string, (decimal Prompt, decimal Completion)>> GetPricesAsync(CancellationToken cancellationToken)
    {
        if (timeProvider.GetUtcNow() - _loadedAt < CacheDuration)
        {
            return _prices;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (timeProvider.GetUtcNow() - _loadedAt < CacheDuration)
            {
                return _prices;
            }

            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetFromJsonAsync<ModelsResponse>($"{options.Value.BaseUrl.TrimEnd('/')}/models", cancellationToken);
            _prices = (response?.Data ?? [])
                .Where(m => m.Pricing is not null)
                .GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (Parse(g.First().Pricing!.Prompt), Parse(g.First().Pricing!.Completion)), StringComparer.OrdinalIgnoreCase);
            _loadedAt = timeProvider.GetUtcNow();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Could not load OpenRouter model pricing; costs will be unknown.");
            _loadedAt = timeProvider.GetUtcNow() - CacheDuration + TimeSpan.FromMinutes(5); // retry in 5 minutes
        }
        finally
        {
            _lock.Release();
        }

        return _prices;
    }

    private static decimal Parse(string? value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private sealed record ModelsResponse([property: JsonPropertyName("data")] List<ModelInfo>? Data);

    private sealed record ModelInfo(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("pricing")] ModelPrice? Pricing);

    private sealed record ModelPrice(
        [property: JsonPropertyName("prompt")] string? Prompt,
        [property: JsonPropertyName("completion")] string? Completion);
}
