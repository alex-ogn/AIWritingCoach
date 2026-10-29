using AIWritingCoach.Core.Common;
using AIWritingCoach.Core.Diff;
using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Services;
using AIWritingCoach.Core.Writing;
using Microsoft.Extensions.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AIWritingCoach.Infrastructure.Export;

/// <summary>
/// Renders a checked text as a PDF: the original, the corrected version, or the original with every
/// change marked plus the review's feedback, scores, mistakes and vocabulary. Labels come from the
/// host's localizer (<c>Pdf.*</c>, <c>Panel.*</c>, <c>Severity.*</c> keys) in the current UI culture.
/// </summary>
public sealed class ReviewPdfExporter(IStringLocalizer localizer)
{
    private const string Removed = "#C62828";
    private const string Added = "#2E7D32";
    private const string RejectedColor = "#9E9E9E";
    private const string Muted = "#616161";
    private const string Rule = "#E0E0E0";

    static ReviewPdfExporter()
    {
        // Free for individuals and organisations under USD 1M annual revenue: https://www.questpdf.com/license/
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Export(WritingDocument document, ReviewSession session, ReviewExportKind kind) =>
        Create(document, session, kind).GeneratePdf();

    /// <summary>The unrendered document, e.g. for <c>GenerateImages</c> previews.</summary>
    public IDocument Create(WritingDocument document, ReviewSession session, ReviewExportKind kind)
    {
        var review = session.Review;
        var heading = string.IsNullOrWhiteSpace(document.Title) ? localizer["Pdf.Untitled"].Value : document.Title.Trim();
        var text = kind == ReviewExportKind.Corrected ? session.Diff.BuildText() : review.SubmittedText;

        return Document
            .Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.4f));

                page.Header().PaddingBottom(12).Column(column =>
                {
                    column.Item().Text(heading).FontSize(18).SemiBold();
                    column.Item().Text(Caption(review, kind, text)).FontSize(9).FontColor(Muted);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(14);
                    if (kind == ReviewExportKind.Annotated)
                    {
                        ComposeAnnotated(column, document.TaskPrompt, session);
                    }
                    else
                    {
                        column.Item().Text(text);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            }))
            .WithMetadata(new DocumentMetadata
            {
                Title = $"{heading} – {localizer[$"Pdf.Kind.{kind}"]}",
                Creator = "AI Writing Coach",
                CreationDate = new DateTimeOffset(DateTime.SpecifyKind(review.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
            });
    }

    private string Caption(Review review, ReviewExportKind kind, string text) =>
        localizer["Pdf.Caption", localizer[$"Pdf.Kind.{kind}"], review.CreatedAt.ToLocalTime().ToString("g"), TextStats.CountWords(text)];

    private void ComposeAnnotated(ColumnDescriptor column, string? taskPrompt, ReviewSession session)
    {
        var review = session.Review;
        var errors = review.Errors.OrderBy(e => e.Index).ToList();
        var numbers = errors.Select((e, i) => (e.Index, Number: i + 1)).ToDictionary(x => x.Index, x => x.Number);

        column.Item().Text(localizer["Pdf.Levels", review.TargetLevel, review.EstimatedLevel?.ToString() ?? "–"]).SemiBold();

        if (!string.IsNullOrWhiteSpace(taskPrompt))
        {
            Section(column, localizer["Pdf.Task"]);
            column.Item().Text(taskPrompt).Italic();
        }

        Section(column, localizer["Pdf.MarkedText"]);
        column.Item().Text(localizer["Pdf.Legend"]).FontSize(8).FontColor(Muted);
        column.Item().Text(t =>
        {
            foreach (var segment in session.Diff.Segments)
            {
                if (segment.Hunk is not { } hunk)
                {
                    t.Span(segment.Text);
                    continue;
                }

                if (hunk.OriginalText.Length > 0)
                {
                    t.Span(hunk.OriginalText).Strikethrough().FontColor(hunk.Accepted ? Removed : RejectedColor);
                }

                if (hunk.CorrectedText.Length > 0)
                {
                    if (hunk.OriginalText.Length > 0)
                    {
                        t.Span(" "); // Keeps "goed went" from reading as one word.
                    }

                    t.Span(hunk.CorrectedText).Underline().FontColor(hunk.Accepted ? Added : RejectedColor);
                }

                var marks = hunk.ErrorIndexes.Where(numbers.ContainsKey).Select(i => numbers[i]).Distinct().ToList();
                if (marks.Count > 0)
                {
                    t.Span(string.Join(",", marks)).Superscript().FontColor(Muted);
                }
            }
        });

        Section(column, localizer["Panel.Feedback"]);
        column.Item().Text(review.OverallFeedback);

        if (review.Scores.Count > 0)
        {
            Section(column, review.ScorePercent is { } percent
                ? localizer["Pdf.ScoresTotal", review.TotalScore, review.MaxTotalScore, percent]
                : localizer["Panel.Scores"]);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.ConstantColumn(50);
                    c.RelativeColumn(5);
                });

                foreach (var score in review.Scores)
                {
                    table.Cell().Element(Row).Text(score.CriterionName).SemiBold();
                    table.Cell().Element(Row).Text($"{score.Score}/{score.MaxScore}");
                    table.Cell().Element(Row).Text(score.Comment ?? string.Empty).FontSize(10);
                }
            });
        }

        Section(column, localizer["Panel.Errors", errors.Count]);
        if (errors.Count == 0)
        {
            column.Item().Text(localizer["Panel.NoErrors"]);
        }

        foreach (var error in errors)
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(24).Text($"{numbers[error.Index]}.").SemiBold();
                row.RelativeItem().Column(item =>
                {
                    item.Item().Text(t =>
                    {
                        if (error.Original.Length > 0)
                        {
                            t.Span(error.Original).Strikethrough().FontColor(Removed);
                            t.Span(" → ");
                        }

                        t.Span(error.Correction.Length > 0 ? error.Correction : localizer["Panel.Removed"].Value).FontColor(Added).SemiBold();
                    });
                    item.Item().Text(ErrorLabel(error)).FontSize(9).FontColor(Muted);
                    item.Item().Text(error.Explanation).FontSize(10);
                });
            });
        }

        if (review.VocabularySuggestions.Count > 0)
        {
            Section(column, localizer["Panel.Vocabulary"]);
            foreach (var vocab in review.VocabularySuggestions.OrderBy(v => v.Index))
            {
                column.Item().Column(item =>
                {
                    item.Item().Text(t =>
                    {
                        if (!string.IsNullOrWhiteSpace(vocab.Original))
                        {
                            t.Span(vocab.Original);
                            t.Span(" → ");
                        }

                        t.Span(vocab.Suggestion).SemiBold();
                        if (vocab.CefrLevel is { } level)
                        {
                            t.Span($"  ({level})").FontSize(9).FontColor(Muted);
                        }
                    });
                    if (!string.IsNullOrWhiteSpace(vocab.Example))
                    {
                        item.Item().Text(vocab.Example).Italic().FontSize(10).FontColor(Muted);
                    }
                });
            }
        }
    }

    private string ErrorLabel(ReviewError error)
    {
        var parts = new List<string> { error.Category };
        if (!string.IsNullOrWhiteSpace(error.Subcategory))
        {
            parts.Add(error.Subcategory);
        }

        parts.Add(localizer[$"Severity.{error.Severity}"]);
        return string.Join(" · ", parts);
    }

    private static void Section(ColumnDescriptor column, string title) =>
        column.Item().PaddingTop(6).BorderBottom(1).BorderColor(Rule).PaddingBottom(2).Text(title).FontSize(13).SemiBold();

    private static IContainer Row(IContainer cell) => cell.BorderBottom(1).BorderColor(Rule).PaddingVertical(4).PaddingRight(6);
}
