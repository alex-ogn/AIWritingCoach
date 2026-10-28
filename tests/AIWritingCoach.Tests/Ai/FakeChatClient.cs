using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;

namespace AIWritingCoach.Tests.Ai;

/// <summary>Scripted <see cref="IChatClient"/>: each call runs the next handler.</summary>
internal sealed class FakeChatClient(params Func<ChatOptions?, ChatResponse>[] handlers) : IChatClient
{
    private int _next;

    public List<string?> RequestedModels { get; } = [];

    public List<int> MessageCounts { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        RequestedModels.Add(options?.ModelId);
        MessageCounts.Add(messages.Count());
        var handler = handlers[Math.Min(_next++, handlers.Length - 1)];
        return Task.FromResult(handler(options));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    public static ChatResponse Text(string text, string? model = null) =>
        new(new ChatMessage(ChatRole.Assistant, text)) { ModelId = model };

    public static ClientResultException HttpError(int status, string message = "error") =>
        new(message, new FakeResponse(status));

    private sealed class FakeResponse(int status) : PipelineResponse
    {
        public override int Status => status;

        public override string ReasonPhrase => "Fake";

        public override Stream? ContentStream { get; set; }

        public override BinaryData Content => BinaryData.FromString(string.Empty);

        protected override PipelineResponseHeaders HeadersCore { get; } = new FakeHeaders();

        public override BinaryData BufferContent(CancellationToken cancellationToken = default) => Content;

        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Content);

        public override void Dispose()
        {
        }
    }

    private sealed class FakeHeaders : PipelineResponseHeaders
    {
        public override bool TryGetValue(string name, out string? value)
        {
            value = null;
            return false;
        }

        public override bool TryGetValues(string name, out IEnumerable<string>? values)
        {
            values = null;
            return false;
        }

        public override IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
            Enumerable.Empty<KeyValuePair<string, string>>().GetEnumerator();
    }
}
