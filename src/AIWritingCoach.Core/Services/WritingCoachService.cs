using AIWritingCoach.Core.Ai;
using AIWritingCoach.Core.Common;
using AIWritingCoach.Core.Diff;
using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Writing;

namespace AIWritingCoach.Core.Services;

/// <summary>Options for one check, usually derived from a profile and the Criteria flyout.</summary>
public sealed record CheckOptions
{
    public Guid? ProfileId { get; init; }

    public Guid? RubricId { get; init; }

    public required IReadOnlyList<RubricCriterion> Criteria { get; init; }

    public string? Model { get; init; }

    public int? MinWords { get; init; }

    public int? MaxWords { get; init; }

    public string FeedbackLanguage { get; init; } = "English";
}

/// <summary>A review together with its locally computed diff.</summary>
public sealed record ReviewSession(Review Review, DiffDocument Diff);

/// <summary>Orchestrates the write → check → accept/reject → apply loop.</summary>
public sealed class WritingCoachService(
    IAiReviewService aiReviewService,
    ITextDiffService diffService,
    IDocumentRepository documents,
    IReviewRepository reviews)
{
    public async Task<ReviewSession> CheckAsync(WritingDocument document, CheckOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(document.Content))
        {
            throw new ArgumentException("The document is empty.", nameof(document));
        }

        document.WordCount = TextStats.CountWords(document.Content);
        await documents.SaveAsync(document, cancellationToken);

        var response = await aiReviewService.ReviewAsync(
            new ReviewRequest
            {
                Text = document.Content,
                TaskType = document.TaskType,
                TargetLevel = document.TargetLevel,
                TaskPrompt = document.TaskPrompt,
                Criteria = options.Criteria,
                MinWords = options.MinWords,
                MaxWords = options.MaxWords,
                Model = options.Model,
                FeedbackLanguage = options.FeedbackLanguage,
            },
            cancellationToken);

        var review = ReviewMapper.ToReview(document, options, response);
        await reviews.AddAsync(review, cancellationToken);

        document.LastReviewId = review.Id;
        await documents.SaveAsync(document, cancellationToken);

        return Open(review);
    }

    /// <summary>Recomputes the diff of a stored review and restores the saved decisions.</summary>
    public ReviewSession Open(Review review)
    {
        var diff = diffService.Compute(review.SubmittedText, review.CorrectedText, review.Errors);
        diff.ApplyRejections(review.RejectedHunkIndexes);
        return new ReviewSession(review, diff);
    }

    public Task SaveDecisionsAsync(ReviewSession session, CancellationToken cancellationToken = default)
    {
        session.Review.RejectedHunkIndexes = [.. session.Diff.RejectedIndexes];
        return reviews.SaveDecisionsAsync(session.Review.Id, session.Review.RejectedHunkIndexes, session.Review.FinalText, cancellationToken);
    }

    /// <summary>Writes the accepted changes back into the document.</summary>
    public async Task ApplyAsync(WritingDocument document, ReviewSession session, CancellationToken cancellationToken = default)
    {
        var finalText = session.Diff.BuildText();
        session.Review.FinalText = finalText;
        await SaveDecisionsAsync(session, cancellationToken);

        document.Content = finalText;
        document.WordCount = TextStats.CountWords(finalText);
        await documents.SaveAsync(document, cancellationToken);
    }
}

public static class ReviewMapper
{
    public static Review ToReview(WritingDocument document, CheckOptions options, AiReviewResponse response)
    {
        var result = response.Result;
        var review = new Review
        {
            UserId = document.UserId,
            DocumentId = document.Id,
            ProfileId = options.ProfileId,
            RubricId = options.RubricId,
            TargetLevel = document.TargetLevel,
            TaskType = document.TaskType,
            SubmittedText = document.Content,
            CorrectedText = result.CorrectedText,
            EstimatedLevel = CefrLevelParser.TryParse(result.EstimatedLevel),
            OverallFeedback = result.OverallFeedback.Trim(),
            WordCount = document.WordCount,
            Model = response.Model,
            PromptTemplateKey = response.PromptTemplateKey,
            PromptTemplateVersion = response.PromptTemplateVersion,
        };

        review.Errors = result.Errors
            .Select((e, i) => new ReviewError
            {
                ReviewId = review.Id,
                Index = i,
                Original = e.Original,
                Correction = e.Correction,
                Category = e.Category.Trim(),
                Subcategory = string.IsNullOrWhiteSpace(e.Subcategory) ? null : e.Subcategory.Trim(),
                Explanation = e.Explanation.Trim(),
                Severity = ParseSeverity(e.Severity),
            })
            .ToList();

        review.Scores = options.Criteria
            .Select(c => (Criterion: c, Score: result.Scores.FirstOrDefault(s => string.Equals(s.Criterion, c.Key, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Score is not null)
            .Select(x => new CriterionScore
            {
                ReviewId = review.Id,
                CriterionKey = x.Criterion.Key,
                CriterionName = x.Criterion.Name,
                Score = Math.Clamp(x.Score!.Score, x.Criterion.MinScore, x.Criterion.MaxScore),
                MaxScore = x.Criterion.MaxScore,
                Comment = x.Score.Comment,
            })
            .ToList();

        review.TotalScore = review.Scores.Sum(s => s.Score);
        review.MaxTotalScore = review.Scores.Sum(s => s.MaxScore);

        review.VocabularySuggestions = result.VocabularySuggestions
            .Where(v => !string.IsNullOrWhiteSpace(v.Suggestion))
            .Select((v, i) => new VocabularySuggestion
            {
                ReviewId = review.Id,
                Index = i,
                Original = v.Original,
                Suggestion = v.Suggestion,
                CefrLevel = CefrLevelParser.TryParse(v.CefrLevel),
                Example = v.Example,
            })
            .ToList();

        return review;
    }

    public static ErrorSeverity ParseSeverity(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "minor" or "low" => ErrorSeverity.Minor,
        "major" or "high" or "critical" or "severe" => ErrorSeverity.Major,
        _ => ErrorSeverity.Moderate,
    };
}
