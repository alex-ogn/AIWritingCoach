using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Writing;

namespace AIWritingCoach.Tests.Services;

public class ProgressReportTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Review Check(Guid document, int day, int score, CefrLevel level, int words, params string[] categories) => new()
    {
        DocumentId = document,
        CreatedAt = Start.AddDays(day),
        TotalScore = score,
        MaxTotalScore = 20,
        EstimatedLevel = level,
        TargetLevel = CefrLevel.C1,
        WordCount = words,
        Scores = [new CriterionScore { CriterionName = "Language", Score = score, MaxScore = 20 }],
        Errors = [.. categories.Select(c => new ReviewError { Category = c, Subcategory = c == "Grammar" ? "Articles" : null })],
    };

    [Fact]
    public void Counts_only_first_check_of_each_text_by_default()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var reviews = new[]
        {
            Check(a, 0, 10, CefrLevel.B1, 100, "Grammar", "Grammar", "Spelling", "Spelling"),
            Check(a, 1, 20, CefrLevel.C1, 100), // re-check of corrected text
            Check(b, 2, 16, CefrLevel.B2, 200, "Grammar", "Spelling"),
        };

        var report = ProgressReport.Build(reviews);

        Assert.Equal((2, 3, 300), (report.Texts, report.Checks, report.Words));
        Assert.Equal([50.0, 80.0], report.ScoreTrend.Select(p => p.Value));
        Assert.Equal([CefrLevel.B1, CefrLevel.B2], report.LevelTrend.Select(p => p.Level));
        Assert.Equal(new ScoreComparison(50, 80, 1), report.Score);
        Assert.Equal(new ScoreComparison(4, 1, 1), report.MistakesPer100Words);

        var grammar = report.MistakeCategories[0];
        Assert.Equal(("Grammar", 3, 50.0), (grammar.Category, grammar.Count, grammar.Share));
        Assert.Equal(("Articles", 3), Assert.Single(grammar.TopSubcategories));

        var language = Assert.Single(report.Criteria);
        Assert.Equal((65.0, 80.0), (language.AveragePercent, language.LatestPercent));
    }

    [Fact]
    public void Includes_rechecks_when_asked()
    {
        var a = Guid.NewGuid();
        var report = ProgressReport.Build([Check(a, 0, 10, CefrLevel.B1, 100), Check(a, 1, 20, CefrLevel.C1, 100)], includeRechecks: true);

        Assert.Equal([50.0, 100.0], report.ScoreTrend.Select(p => p.Value));
        Assert.Equal(200, report.Words);
    }

    [Fact]
    public void No_comparison_with_fewer_than_two_checks()
    {
        var report = ProgressReport.Build([Check(Guid.NewGuid(), 0, 10, CefrLevel.B1, 100)]);

        Assert.Null(report.Score);
        Assert.Null(report.MistakesPer100Words);
    }
}
