using System.Text;
using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Services;
using AIWritingCoach.Core.Writing;
using AIWritingCoach.Infrastructure.Diff;
using AIWritingCoach.Infrastructure.Export;
using Microsoft.Extensions.Localization;

namespace AIWritingCoach.Tests.Export;

public class ReviewPdfExporterTests
{
    private readonly ReviewPdfExporter _exporter = new(new KeyLocalizer());

    [Theory]
    [InlineData(ReviewExportKind.Original)]
    [InlineData(ReviewExportKind.Corrected)]
    [InlineData(ReviewExportKind.Annotated)]
    public void Export_produces_a_pdf_for_every_kind(ReviewExportKind kind)
    {
        var (document, session) = Sample("She go to school every days.", "She goes to school every day.");

        var pdf = _exporter.Export(document, session, kind);

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Fact]
    public void Annotated_export_handles_cyrillic_empty_fields_and_rejected_changes()
    {
        var (document, session) = Sample("Аз отидох до училище вчера.\n\nВтори абзац.", "Аз отидох на училище вчера.\n\nВтори абзац.");
        document.Title = "";
        document.TaskPrompt = "Напишете есе.";
        session.Review.Errors.Add(new ReviewError { Index = 1, Original = "", Correction = "", Category = "Other", Explanation = "" });
        session.Review.Scores.Clear();
        session.Review.VocabularySuggestions.Clear();
        session.Diff.SetAll(false);

        var pdf = _exporter.Export(document, session, ReviewExportKind.Annotated);

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Theory]
    [InlineData("My essay", ReviewExportKind.Corrected, "My essay - corrected.pdf")]
    [InlineData("  Dear Sir/Madam: \"complaint\"?  ", ReviewExportKind.Original, "Dear Sir Madam complaint - original.pdf")]
    [InlineData("Есе за природата", ReviewExportKind.Annotated, "Есе за природата - annotated.pdf")]
    [InlineData("", ReviewExportKind.Original, "text - original.pdf")]
    [InlineData(null, ReviewExportKind.Annotated, "text - annotated.pdf")]
    public void File_name_is_safe(string? title, ReviewExportKind kind, string expected)
    {
        Assert.Equal(expected, ReviewExportFile.Name(title, kind));
    }

    [Fact]
    public void File_name_truncates_long_titles()
    {
        var name = ReviewExportFile.Name(new string('a', 200), ReviewExportKind.Original);

        Assert.Equal(new string('a', 60) + " - original.pdf", name);
    }

    private static (WritingDocument Document, ReviewSession Session) Sample(string original, string corrected)
    {
        var document = new WritingDocument { Title = "My essay", Content = original, TaskPrompt = "Describe your school day." };
        var review = new Review
        {
            DocumentId = document.Id,
            SubmittedText = original,
            CorrectedText = corrected,
            TargetLevel = CefrLevel.B2,
            EstimatedLevel = CefrLevel.B1,
            OverallFeedback = "Clear ideas; watch verb agreement.",
            TotalScore = 7,
            MaxTotalScore = 10,
            Errors =
            [
                new ReviewError { Index = 0, Original = "go", Correction = "goes", Category = "Grammar", Subcategory = "Subject-verb agreement", Explanation = "Third person singular takes -s.", Severity = ErrorSeverity.Major },
            ],
            Scores = [new CriterionScore { CriterionName = "Language", Score = 7, MaxScore = 10, Comment = "Mostly accurate." }],
            VocabularySuggestions = [new VocabularySuggestion { Original = "go", Suggestion = "attend", CefrLevel = CefrLevel.B2, Example = "She attends school." }],
        };

        var diff = new DiffPlexTextDiffService().Compute(original, corrected, review.Errors);
        return (document, new ReviewSession(review, diff));
    }

    /// <summary>Returns the key (and any arguments) so labels never fail to resolve.</summary>
    private sealed class KeyLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, $"{name} {string.Join(" ", arguments)}");

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
