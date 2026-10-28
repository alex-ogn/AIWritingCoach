using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Writing;

namespace AIWritingCoach.Core.Ai;

/// <summary>
/// Checks a parsed <see cref="ReviewResult"/> against the request. The returned messages are
/// fed back to the model on a repair retry, so they are phrased as instructions.
/// </summary>
public static class ReviewResultValidator
{
    public static IReadOnlyList<string> Validate(ReviewResult? result, IReadOnlyList<RubricCriterion> criteria)
    {
        if (result is null)
        {
            return ["The response was empty or not a JSON object."];
        }

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(result.CorrectedText))
        {
            problems.Add("'correctedText' must contain the full corrected text.");
        }

        if (CefrLevelParser.TryParse(result.EstimatedLevel) is null)
        {
            problems.Add("'estimatedLevel' must be one of A1, A2, B1, B2, C1, C2.");
        }

        if (string.IsNullOrWhiteSpace(result.OverallFeedback))
        {
            problems.Add("'overallFeedback' must not be empty.");
        }

        for (var i = 0; i < result.Errors.Count; i++)
        {
            var error = result.Errors[i];
            if (string.IsNullOrWhiteSpace(error.Original) && string.IsNullOrWhiteSpace(error.Correction))
            {
                problems.Add($"errors[{i}] must have 'original' or 'correction'.");
            }

            if (string.IsNullOrWhiteSpace(error.Category))
            {
                problems.Add($"errors[{i}].category must not be empty.");
            }
        }

        foreach (var criterion in criteria)
        {
            var score = result.Scores.FirstOrDefault(s => string.Equals(s.Criterion, criterion.Key, StringComparison.OrdinalIgnoreCase));
            if (score is null)
            {
                problems.Add($"'scores' is missing criterion '{criterion.Key}'.");
            }
            else if (score.Score < criterion.MinScore || score.Score > criterion.MaxScore)
            {
                problems.Add($"Score for '{criterion.Key}' must be between {criterion.MinScore} and {criterion.MaxScore}.");
            }
        }

        return problems;
    }
}
