using System.ClientModel;
using AIWritingCoach.Infrastructure.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIWritingCoach.Tests.Ai;

public class ModelFallbackChatClientTests
{
    private static readonly OpenRouterOptions Options = new()
    {
        DefaultModel = "a:free",
        FallbackModels = ["b:free", "c-paid"],
        MaxRetriesPerModel = 1,
        BaseRetryDelay = TimeSpan.Zero,
    };

    private static ModelFallbackChatClient Create(FakeChatClient inner) =>
        new(inner, Options, TimeProvider.System, NullLogger<ModelFallbackChatClient>.Instance);

    [Fact]
    public async Task Retries_a_rate_limited_model_then_falls_back()
    {
        var inner = new FakeChatClient(
            _ => throw FakeChatClient.HttpError(429),
            _ => throw FakeChatClient.HttpError(429),
            o => FakeChatClient.Text("ok", o?.ModelId));

        var response = await Create(inner).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("ok", response.Text);
        Assert.Equal(["a:free", "a:free", "b:free"], inner.RequestedModels);
        Assert.Equal("b:free", response.ModelId);
    }

    [Fact]
    public async Task Daily_free_quota_skips_straight_to_the_next_model()
    {
        var inner = new FakeChatClient(
            _ => throw FakeChatClient.HttpError(429, "Rate limit exceeded: free-models-per-day"),
            o => FakeChatClient.Text("ok", o?.ModelId));

        await Create(inner).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal(["a:free", "b:free"], inner.RequestedModels);
    }

    [Fact]
    public async Task Requested_model_is_tried_first()
    {
        var inner = new FakeChatClient(o => FakeChatClient.Text("ok", o?.ModelId));

        await Create(inner).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { ModelId = "custom" });

        Assert.Equal(["custom"], inner.RequestedModels);
    }

    [Fact]
    public async Task Bad_request_is_not_retried()
    {
        var inner = new FakeChatClient(_ => throw FakeChatClient.HttpError(400));

        await Assert.ThrowsAsync<ClientResultException>(() => Create(inner).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));
        Assert.Single(inner.RequestedModels);
    }

    [Fact]
    public async Task Throws_rate_limited_when_every_model_is_exhausted()
    {
        var inner = new FakeChatClient(_ => throw FakeChatClient.HttpError(429));

        var ex = await Assert.ThrowsAsync<AiProviderUnavailableException>(() => Create(inner).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        Assert.True(ex.RateLimited);
        Assert.Equal(6, inner.RequestedModels.Count); // 3 models × (1 try + 1 retry)
    }
}
