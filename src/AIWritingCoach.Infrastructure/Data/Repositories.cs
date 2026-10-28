using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Services;
using AIWritingCoach.Core.Usage;
using AIWritingCoach.Core.Writing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIWritingCoach.Infrastructure.Data;

internal sealed class DocumentRepository(IAppDbContextFactory factory) : IDocumentRepository
{
    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        var rows = await db.Documents
            .AsNoTracking()
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new
            {
                d.Id,
                d.Title,
                d.TaskType,
                d.TargetLevel,
                d.WordCount,
                d.UpdatedAt,
                Last = db.Reviews
                    .Where(r => r.Id == d.LastReviewId)
                    .Select(r => new { r.TotalScore, r.MaxTotalScore, r.EstimatedLevel })
                    .FirstOrDefault(),
                ReviewCount = db.Reviews.Count(r => r.DocumentId == d.Id),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new DocumentSummary(
                r.Id,
                r.Title,
                r.TaskType,
                r.TargetLevel,
                r.WordCount,
                r.UpdatedAt,
                r.Last is null ? null : ScoreMath.Percent(r.Last.TotalScore, r.Last.MaxTotalScore),
                r.Last?.EstimatedLevel,
                r.ReviewCount))
            .ToList();
    }

    public async Task<WritingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        return await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Guid?> GetLastUsedProfileIdAsync(CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        return await db.Documents
            .Where(d => d.ProfileId != null)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => d.ProfileId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(WritingDocument document, CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        if (await db.Documents.AnyAsync(d => d.Id == document.Id, cancellationToken))
        {
            db.Documents.Update(document);
        }
        else
        {
            db.Documents.Add(document);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is not null)
        {
            db.Documents.Remove(document);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

internal sealed class ReviewRepository(IAppDbContextFactory factory) : IReviewRepository
{
    public async Task<Review?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        var review = await db.Reviews
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Errors)
            .Include(r => r.Scores)
            .Include(r => r.VocabularySuggestions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (review is not null)
        {
            review.Errors = [.. review.Errors.OrderBy(e => e.Index)];
            review.VocabularySuggestions = [.. review.VocabularySuggestions.OrderBy(v => v.Index)];
        }

        return review;
    }

    public async Task<IReadOnlyList<Review>> ListForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        return await db.Reviews
            .AsNoTracking()
            .Where(r => r.DocumentId == documentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Review review, CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        db.Reviews.Add(review);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveDecisionsAsync(Guid reviewId, IReadOnlyList<int> rejectedHunkIndexes, string? finalText, CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
            ?? throw new InvalidOperationException($"Review {reviewId} not found.");
        review.RejectedHunkIndexes = [.. rejectedHunkIndexes];
        review.FinalText = finalText;
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class RubricRepository(IAppDbContextFactory factory) : IRubricRepository
{
    public async Task<IReadOnlyList<Rubric>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        return await db.Rubrics.AsNoTracking().OrderBy(r => r.Kind).ThenBy(r => r.Name).ToListAsync(cancellationToken);
    }
}

internal sealed class ProfileRepository(IAppDbContextFactory factory) : IProfileRepository
{
    public async Task<IReadOnlyList<Profile>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        return await db.Profiles.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(cancellationToken);
    }
}

internal sealed class PromptTemplateRepository(IAppDbContextFactory factory) : IPromptTemplateRepository
{
    public async Task<PromptTemplate?> GetLatestAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var db = factory.Create();
        return await db.PromptTemplates
            .AsNoTracking()
            .Where(t => t.Key == key)
            .OrderByDescending(t => t.Version)
            .ThenByDescending(t => t.UserId != null)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>Writes usage rows; never lets logging failures break a review.</summary>
internal sealed class UsageLogger(IAppDbContextFactory factory, ILogger<UsageLogger> logger) : IUsageLogger
{
    public async Task LogAsync(AiUsageLog entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = factory.Create();
            db.AiUsageLogs.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to write AI usage log for model {Model}", entry.Model);
        }
    }
}
