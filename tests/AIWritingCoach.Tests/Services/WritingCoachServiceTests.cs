using AIWritingCoach.Core.Ai;
using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Services;
using AIWritingCoach.Core.Writing;
using AIWritingCoach.Infrastructure.Ai;
using AIWritingCoach.Infrastructure.Diff;
using AIWritingCoach.Tests.Ai;

namespace AIWritingCoach.Tests.Services;

public class WritingCoachServiceTests
{
    private readonly InMemoryDocuments _documents = new();
    private readonly InMemoryReviews _reviews = new();

    private WritingCoachService Create(ReviewResult result) =>
        new(new FixedAi(result), new DiffPlexTextDiffService(), _documents, _reviews);

    [Fact]
    public async Task Check_saves_review_maps_scores_and_links_errors()
    {
        var result = ReviewJson.TryParse(ReviewParsingTests.ValidJson, out _)!;
        var document = new WritingDocument { Content = "She go to school." };

        var session = await Create(result).CheckAsync(document, new CheckOptions { Criteria = [ReviewParsingTests.Language] });

        Assert.Equal(session.Review.Id, document.LastReviewId);
        Assert.Same(session.Review, Assert.Single(_reviews.Items));
        Assert.Equal(CefrLevel.B1, session.Review.EstimatedLevel);
        Assert.Equal(ErrorSeverity.Major, Assert.Single(session.Review.Errors).Severity);
        Assert.Equal((3, 5), (session.Review.TotalScore, session.Review.MaxTotalScore));
        Assert.Equal([0], Assert.Single(session.Diff.Hunks).ErrorIndexes);
    }

    [Fact]
    public async Task Apply_writes_accepted_changes_back_and_persists_decisions()
    {
        var result = ReviewJson.TryParse(ReviewParsingTests.ValidJson, out _)!;
        result.CorrectedText = "She goes to the school.";
        var document = new WritingDocument { Content = "She go to school." };
        var service = Create(result);
        var session = await service.CheckAsync(document, new CheckOptions { Criteria = [] });

        session.Diff.Hunks[1].Accepted = false; // reject "the"
        await service.ApplyAsync(document, session);

        Assert.Equal("She goes to school.", document.Content);
        Assert.Equal([1], _reviews.Items[0].RejectedHunkIndexes);

        var reopened = service.Open(_reviews.Items[0]);
        Assert.False(reopened.Diff.Hunks[1].Accepted);
    }

    private sealed class FixedAi(ReviewResult result) : IAiReviewService
    {
        public Task<AiReviewResponse> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiReviewResponse(result, "test-model", "review", 1, 1));
    }

    private sealed class InMemoryDocuments : IDocumentRepository
    {
        public List<WritingDocument> Items { get; } = [];

        public Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WritingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(d => d.Id == id));

        public Task SaveAsync(WritingDocument document, CancellationToken cancellationToken = default)
        {
            if (!Items.Contains(document))
            {
                Items.Add(document);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class InMemoryReviews : IReviewRepository
    {
        public List<Review> Items { get; } = [];

        public Task<Review?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Review>> ListForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Review>>(Items.Where(r => r.DocumentId == documentId).ToList());

        public Task AddAsync(Review review, CancellationToken cancellationToken = default)
        {
            Items.Add(review);
            return Task.CompletedTask;
        }

        public Task SaveDecisionsAsync(Guid reviewId, IReadOnlyList<int> rejectedHunkIndexes, string? finalText, CancellationToken cancellationToken = default)
        {
            var review = Items.Single(r => r.Id == reviewId);
            review.RejectedHunkIndexes = [.. rejectedHunkIndexes];
            review.FinalText = finalText;
            return Task.CompletedTask;
        }
    }
}
