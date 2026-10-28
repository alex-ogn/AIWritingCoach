using System.ClientModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AIWritingCoach.Core.Ai;
using AIWritingCoach.Core.Common;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIWritingCoach.Infrastructure.Ai;

/// <summary>
/// Renders the review prompt, requests structured JSON, validates it and retries with a repair
/// message when parsing or validation fails.
/// </summary>
public sealed class AiReviewService(
    IChatClient chatClient,
    IPromptTemplateRepository prompts,
    IPromptRenderer renderer,
    IOptions<OpenRouterOptions> options,
    ILogger<AiReviewService> logger) : IAiReviewService
{
    private static readonly Lazy<string> Schema = new(() =>
        AIJsonUtilities.CreateJsonSchema(typeof(ReviewResult), serializerOptions: ReviewJson.Options).GetRawText());

    public async Task<AiReviewResponse> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw new AiReviewException(AiReviewFailure.NotConfigured, "OpenRouter API key is not configured.");
        }

        var template = await prompts.GetLatestAsync(PromptTemplate.ReviewKey, cancellationToken)
            ?? throw new InvalidOperationException($"Prompt template '{PromptTemplate.ReviewKey}' is missing; was the database seeded?");

        var values = BuildValues(request);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, renderer.Render(template.SystemPrompt, values)),
            new(ChatRole.User, renderer.Render(template.UserTemplate, values)),
        };

        var chatOptions = new ChatOptions
        {
            ModelId = request.Model,
            Temperature = settings.Temperature,
            MaxOutputTokens = settings.MaxOutputTokens,
            ResponseFormat = settings.UseJsonSchema
                ? ChatResponseFormat.ForJsonSchema(JsonDocument.Parse(Schema.Value).RootElement, "review_result")
                : ChatResponseFormat.Json,
        };

        using var feature = AiFeature.Begin("review");
        IReadOnlyList<string> problems = [];

        for (var attempt = 1; attempt <= Math.Max(1, settings.MaxParseAttempts); attempt++)
        {
            var response = await SendAsync(messages, chatOptions, cancellationToken);
            var text = response.Text;

            var result = ReviewJson.TryParse(text, out var parseError);
            problems = parseError is not null ? [parseError] : ReviewResultValidator.Validate(result, request.Criteria);
            if (problems.Count == 0)
            {
                return new AiReviewResponse(result!, response.ModelId ?? request.Model ?? settings.DefaultModel, template.Key, template.Version, attempt);
            }

            logger.LogWarning("Review response invalid (attempt {Attempt}, model {Model}): {Problems}", attempt, response.ModelId, string.Join("; ", problems));
            messages.Add(new ChatMessage(ChatRole.Assistant, text));
            messages.Add(new ChatMessage(ChatRole.User,
                "Your previous response was not valid:\n- " + string.Join("\n- ", problems) +
                "\nReturn the complete, corrected JSON object only, matching the schema."));
        }

        throw new AiReviewException(AiReviewFailure.InvalidResponse,
            "The AI returned an invalid response: " + string.Join("; ", problems));
    }

    private async Task<ChatResponse> SendAsync(List<ChatMessage> messages, ChatOptions chatOptions, CancellationToken cancellationToken)
    {
        try
        {
            return await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        }
        catch (ClientResultException ex) when (ex.Status == 400 && chatOptions.ResponseFormat is ChatResponseFormatJson { Schema: not null })
        {
            // Some (free) models reject json_schema; plain JSON mode + the schema in the prompt still works.
            logger.LogInformation("Model rejected json_schema response format; retrying with JSON mode. {Message}", ex.Message);
            chatOptions.ResponseFormat = ChatResponseFormat.Json;
            return await SendAsync(messages, chatOptions, cancellationToken);
        }
        catch (AiProviderUnavailableException ex)
        {
            throw new AiReviewException(ex.RateLimited ? AiReviewFailure.RateLimited : AiReviewFailure.ProviderError, ex.Message, ex);
        }
        catch (ClientResultException ex)
        {
            throw new AiReviewException(AiReviewFailure.ProviderError, $"The AI provider returned an error ({ex.Status}): {ex.Message}", ex);
        }
    }

    internal static Dictionary<string, string> BuildValues(ReviewRequest request)
    {
        var criteria = new StringBuilder();
        foreach (var c in request.Criteria)
        {
            criteria.Append(CultureInfo.InvariantCulture, $"- {c.Key} ({c.Name}, {c.MinScore}-{c.MaxScore}): {c.Description}\n");
        }

        var limits = (request.MinWords, request.MaxWords) switch
        {
            (int min, int max) => $"{min}-{max} words",
            (int min, null) => $"at least {min} words",
            (null, int max) => $"at most {max} words",
            _ => "none",
        };

        return new Dictionary<string, string>
        {
            ["taskType"] = request.TaskType.ToString(),
            ["targetLevel"] = request.TargetLevel.ToString(),
            ["taskPrompt"] = string.IsNullOrWhiteSpace(request.TaskPrompt) ? "(not given)" : request.TaskPrompt.Trim(),
            ["wordLimits"] = limits,
            ["wordCount"] = TextStats.CountWords(request.Text).ToString(CultureInfo.InvariantCulture),
            ["criteria"] = criteria.Length > 0 ? criteria.ToString().TrimEnd() : "(none - return an empty 'scores' array)",
            ["feedbackLanguage"] = request.FeedbackLanguage,
            ["jsonSchema"] = Schema.Value,
            ["text"] = request.Text,
        };
    }
}

/// <summary>Replaces <c>{{name}}</c> placeholders; unknown placeholders are left as they are.</summary>
public sealed partial class PromptRenderer : IPromptRenderer
{
    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex Placeholder();

    public string Render(string template, IReadOnlyDictionary<string, string> values) =>
        Placeholder().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
}

public static class ReviewJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Parses model output, tolerating markdown fences or text around the JSON object.</summary>
    public static ReviewResult? TryParse(string? text, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "The response was empty.";
            return null;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            error = "The response did not contain a JSON object.";
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<ReviewResult>(text.AsSpan(start, end - start + 1), Options);
            if (result is not null)
            {
                // JSON nulls override the property initializers.
                result.Errors ??= [];
                result.Scores ??= [];
                result.VocabularySuggestions ??= [];
            }

            return result;
        }
        catch (JsonException ex)
        {
            error = $"The JSON could not be parsed: {ex.Message}";
            return null;
        }
    }
}
