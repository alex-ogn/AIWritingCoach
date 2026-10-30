using AIWritingCoach.Core.Ai;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Services;
using AIWritingCoach.Core.Writing;
using AIWritingCoach.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace AIWritingCoach.Tests.Ai;

public class AiReviewServiceTests
{
    private static AiReviewService Create(FakeChatClient chat, string? apiKey = "test-key") => new(
        chat,
        new FakePrompts(),
        new PromptRenderer(),
        MsOptions.Create(new OpenRouterOptions { ApiKey = apiKey, MaxParseAttempts = 2 }),
        NullLogger<AiReviewService>.Instance);

    private static ReviewRequest Request => new() { Text = "She go to school.", Criteria = [ReviewParsingTests.Language] };

    [Fact]
    public async Task Returns_a_valid_result()
    {
        var chat = new FakeChatClient(_ => FakeChatClient.Text(ReviewParsingTests.ValidJson, "m1"));

        var response = await Create(chat).ReviewAsync(Request);

        Assert.Equal("She goes to school.", response.Result.CorrectedText);
        Assert.Equal("m1", response.Model);
        Assert.Equal(1, response.Attempts);
        Assert.Equal(("review", 7), (response.PromptTemplateKey, response.PromptTemplateVersion));
    }

    [Fact]
    public async Task Retries_with_a_repair_message_when_json_is_invalid()
    {
        var chat = new FakeChatClient(
            _ => FakeChatClient.Text("not json"),
            _ => FakeChatClient.Text(ReviewParsingTests.ValidJson));

        var response = await Create(chat).ReviewAsync(Request);

        Assert.Equal(2, response.Attempts);
        Assert.Equal([2, 4], chat.MessageCounts); // system+user, then + invalid answer + repair request
    }

    [Fact]
    public async Task Gives_up_after_the_configured_attempts()
    {
        var chat = new FakeChatClient(_ => FakeChatClient.Text("{}"));

        var ex = await Assert.ThrowsAsync<AiReviewException>(() => Create(chat).ReviewAsync(Request));

        Assert.Equal(AiReviewFailure.InvalidResponse, ex.Reason);
    }

    [Fact]
    public async Task Missing_api_key_fails_fast()
    {
        var chat = new FakeChatClient(_ => FakeChatClient.Text(ReviewParsingTests.ValidJson));

        var ex = await Assert.ThrowsAsync<AiReviewException>(() => Create(chat, apiKey: null).ReviewAsync(Request));

        Assert.Equal(AiReviewFailure.NotConfigured, ex.Reason);
        Assert.Empty(chat.RequestedModels);
    }

    [Fact]
    public void Prompt_values_describe_criteria_and_limits()
    {
        var values = AiReviewService.BuildValues(Request with { MinWords = 140, MaxWords = 190 });

        Assert.Equal("140-190 words", values["wordLimits"]);
        Assert.Equal("4", values["wordCount"]);
        Assert.Contains("language (Language, 0-5)", values["criteria"]);
    }

    [Fact]
    public async Task Suggests_a_cleaned_up_title()
    {
        var chat = new FakeChatClient(_ => FakeChatClient.Text("**Title: \"A Day Without Phones\".**\nHope you like it!"));

        var title = await Create(chat).SuggestTitleAsync(new TitleRequest { Model = "m1" });

        Assert.Equal("A Day Without Phones", title);
        Assert.Equal(["m1"], chat.RequestedModels);
    }

    [Fact]
    public async Task Empty_title_is_an_invalid_response()
    {
        var chat = new FakeChatClient(_ => FakeChatClient.Text("  \"\"  "));

        var ex = await Assert.ThrowsAsync<AiReviewException>(() => Create(chat).SuggestTitleAsync(new TitleRequest()));

        Assert.Equal(AiReviewFailure.InvalidResponse, ex.Reason);
    }

    [Fact]
    public async Task Title_without_api_key_fails_fast()
    {
        var chat = new FakeChatClient(_ => FakeChatClient.Text("Title"));

        var ex = await Assert.ThrowsAsync<AiReviewException>(() => Create(chat, apiKey: null).SuggestTitleAsync(new TitleRequest()));

        Assert.Equal(AiReviewFailure.NotConfigured, ex.Reason);
        Assert.Empty(chat.RequestedModels);
    }

    [Fact]
    public void Title_prompt_describes_the_task()
    {
        var prompt = AiReviewService.BuildTitlePrompt(new TitleRequest
        {
            ProfileName = "C1 Essay",
            TaskType = WritingTaskType.Report,
            TargetLevel = CefrLevel.C1,
            TaskPrompt = "  Write about public transport.  ",
        });

        Assert.Contains("Task type: Report", prompt);
        Assert.Contains("Target level: C1", prompt);
        Assert.Contains("Profile: C1 Essay", prompt);
        Assert.Contains("Task instructions: Write about public transport.", prompt);
    }

    [Fact]
    public void Long_titles_are_shortened()
    {
        var title = AiReviewService.CleanTitle(new string('a', 200));

        Assert.Equal(81, title.Length);
        Assert.EndsWith("…", title);
    }

    private sealed class FakePrompts : IPromptTemplateRepository
    {
        public Task<PromptTemplate?> GetLatestAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<PromptTemplate?>(new PromptTemplate
            {
                Key = key,
                Version = 7,
                SystemPrompt = "System {{feedbackLanguage}} {{jsonSchema}}",
                UserTemplate = "Check: {{text}}",
            });
    }
}
