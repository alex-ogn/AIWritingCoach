using AIWritingCoach.Core.Ai;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Infrastructure.Ai;

namespace AIWritingCoach.Tests.Ai;

public class ReviewParsingTests
{
    internal const string ValidJson = """
        {
          "correctedText": "She goes to school.",
          "estimatedLevel": "B1",
          "overallFeedback": "Good start.",
          "errors": [ { "original": "go", "correction": "goes", "category": "Grammar", "subcategory": "Subject-verb agreement", "explanation": "Third person -s.", "severity": "major" } ],
          "vocabularySuggestions": null,
          "scores": [ { "criterion": "language", "score": "3", "comment": "OK" } ]
        }
        """;

    internal static readonly RubricCriterion Language = new() { Key = "language", Name = "Language", MinScore = 0, MaxScore = 5 };

    [Fact]
    public void Parses_json_wrapped_in_markdown_fences_and_normalizes_nulls()
    {
        var result = ReviewJson.TryParse($"Here you go:\n```json\n{ValidJson}\n```", out var error);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("She goes to school.", result.CorrectedText);
        Assert.Equal(3, Assert.Single(result.Scores).Score);
        Assert.Empty(result.VocabularySuggestions);
        Assert.Empty(ReviewResultValidator.Validate(result, [Language]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sorry, I cannot help with that.")]
    [InlineData("{ \"correctedText\": ")]
    public void Reports_unparseable_responses(string text)
    {
        Assert.Null(ReviewJson.TryParse(text, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Validator_reports_missing_and_out_of_range_scores()
    {
        var result = ReviewJson.TryParse(ValidJson, out _)!;
        result.Scores[0].Score = 9;
        result.EstimatedLevel = "intermediate";

        var problems = ReviewResultValidator.Validate(result, [Language, Language with { Key = "content", Name = "Content" }]);

        Assert.Contains(problems, p => p.Contains("between 0 and 5"));
        Assert.Contains(problems, p => p.Contains("missing criterion 'content'"));
        Assert.Contains(problems, p => p.Contains("estimatedLevel"));
    }

    [Fact]
    public void Prompt_renderer_fills_known_placeholders_only()
    {
        var rendered = new PromptRenderer().Render("Level {{ targetLevel }}, {{unknown}}", new Dictionary<string, string> { ["targetLevel"] = "C1" });

        Assert.Equal("Level C1, {{unknown}}", rendered);
    }
}
