using AIWritingCoach.Core.Writing;

namespace AIWritingCoach.Core.Reviews;

public sealed record ProgressPoint(DateTime CheckedAt, double Value);

public sealed record LevelPoint(DateTime CheckedAt, CefrLevel Level, CefrLevel Target);

public sealed record CriterionProgress(string Name, double AveragePercent, double LatestPercent);

public sealed record MistakeCategoryStat(string Category, int Count, double Share, IReadOnlyList<(string Subcategory, int Count)> TopSubcategories);

/// <summary>Average of the first and of the last <paramref name="Size"/> checks.</summary>
public sealed record ScoreComparison(double First, double Last, int Size);

/// <summary>Aggregates of a user's reviews for the Progress page.</summary>
public sealed record ProgressReport
{
    /// <summary>How many checks are averaged for the "first vs last" comparisons.</summary>
    public const int CompareCount = 5;

    public int Texts { get; init; }

    public int Checks { get; init; }

    public int Words { get; init; }

    public ScoreComparison? Score { get; init; }

    public ScoreComparison? MistakesPer100Words { get; init; }

    public IReadOnlyList<ProgressPoint> ScoreTrend { get; init; } = [];

    public IReadOnlyList<LevelPoint> LevelTrend { get; init; } = [];

    public IReadOnlyList<CriterionProgress> Criteria { get; init; } = [];

    public IReadOnlyList<MistakeCategoryStat> MistakeCategories { get; init; } = [];

    /// <summary>
    /// Builds the report. Unless <paramref name="includeRechecks"/> is set, only the first check of each text
    /// counts: later checks usually run on already-corrected text and would inflate the scores.
    /// </summary>
    /// <param name="reviews">Reviews with their errors and scores loaded.</param>
    public static ProgressReport Build(IEnumerable<Review> reviews, bool includeRechecks = false)
    {
        var all = reviews.OrderBy(r => r.CreatedAt).ToList();
        var counted = includeRechecks
            ? all
            : all.GroupBy(r => r.DocumentId).Select(g => g.First()).OrderBy(r => r.CreatedAt).ToList();

        var scored = counted.Where(r => r.ScorePercent is not null).ToList();
        var withWords = counted.Where(r => r.WordCount > 0).ToList();

        return new ProgressReport
        {
            Texts = all.Select(r => r.DocumentId).Distinct().Count(),
            Checks = all.Count,
            Words = counted.Sum(r => r.WordCount),
            Score = Compare(scored, r => r.ScorePercent!.Value),
            MistakesPer100Words = Compare(withWords, r => 100.0 * r.Errors.Count / r.WordCount),
            ScoreTrend = [.. scored.Select(r => new ProgressPoint(r.CreatedAt, r.ScorePercent!.Value))],
            LevelTrend = [.. counted.Where(r => r.EstimatedLevel is not null).Select(r => new LevelPoint(r.CreatedAt, r.EstimatedLevel!.Value, r.TargetLevel))],
            Criteria = BuildCriteria(counted),
            MistakeCategories = BuildMistakes(counted),
        };
    }

    private static ScoreComparison? Compare(IReadOnlyList<Review> ordered, Func<Review, double> value)
    {
        // Needs two non-overlapping groups to say anything about change.
        if (ordered.Count < 2)
        {
            return null;
        }

        var size = Math.Min(CompareCount, ordered.Count / 2);
        return new ScoreComparison(
            Math.Round(ordered.Take(size).Average(value), 1),
            Math.Round(ordered.TakeLast(size).Average(value), 1),
            size);
    }

    private static List<CriterionProgress> BuildCriteria(IReadOnlyList<Review> ordered) =>
        ordered
            .SelectMany(r => r.Scores.Where(s => s.MaxScore > 0).Select(s => (r.CreatedAt, s.CriterionName, Percent: 100.0 * s.Score / s.MaxScore)))
            .GroupBy(x => x.CriterionName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CriterionProgress(
                g.First().CriterionName,
                Math.Round(g.Average(x => x.Percent), 1),
                Math.Round(g.MaxBy(x => x.CreatedAt).Percent, 1)))
            .OrderBy(c => c.AveragePercent)
            .ToList();

    private static List<MistakeCategoryStat> BuildMistakes(IReadOnlyList<Review> reviews)
    {
        var errors = reviews.SelectMany(r => r.Errors).ToList();
        return errors
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "Other" : e.Category, StringComparer.OrdinalIgnoreCase)
            .Select(g => new MistakeCategoryStat(
                g.Key,
                g.Count(),
                Math.Round(100.0 * g.Count() / errors.Count, 1),
                [.. g.Where(e => !string.IsNullOrWhiteSpace(e.Subcategory))
                    .GroupBy(e => e.Subcategory!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(s => (s.Key, s.Count()))
                    .OrderByDescending(s => s.Item2)
                    .Take(3)]))
            .OrderByDescending(c => c.Count)
            .ToList();
    }
}
