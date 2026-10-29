using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Vocabulary;
using AIWritingCoach.Core.Writing;

namespace AIWritingCoach.Core.Services;

// Thin, LINQ-backed data access. All implementations are scoped to the current user.

public interface IDocumentRepository
{
    Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<WritingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates the document.</summary>
    Task SaveAsync(WritingDocument document, CancellationToken cancellationToken = default);

    /// <summary>Deletes the document together with its reviews.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IReviewRepository
{
    Task<Review?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reviews of a document, newest first, without child collections.</summary>
    Task<IReadOnlyList<Review>> ListForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task AddAsync(Review review, CancellationToken cancellationToken = default);

    Task SaveDecisionsAsync(Guid reviewId, IReadOnlyList<int> rejectedHunkIndexes, string? finalText, CancellationToken cancellationToken = default);
}

public interface IRubricRepository
{
    Task<IReadOnlyList<Rubric>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IProfileRepository
{
    Task<IReadOnlyList<Profile>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IPromptTemplateRepository
{
    /// <summary>The highest version of the template visible to the current user.</summary>
    Task<PromptTemplate?> GetLatestAsync(string key, CancellationToken cancellationToken = default);
}

public interface IVocabularyRepository
{
    /// <summary>All items, newest first.</summary>
    Task<IReadOnlyList<VocabularyItem>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds the items whose word is not in the list yet (case-insensitive); returns the ones added.</summary>
    Task<IReadOnlyList<VocabularyItem>> AddMissingAsync(IReadOnlyList<VocabularyItem> items, CancellationToken cancellationToken = default);

    Task SetStatusAsync(Guid id, VocabularyStatus status, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
