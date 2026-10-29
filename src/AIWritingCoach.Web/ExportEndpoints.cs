using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Services;
using AIWritingCoach.Infrastructure.Export;

namespace AIWritingCoach.Web;

public static class ExportEndpoints
{
    public static string PdfUrl(Guid reviewId, ReviewExportKind kind) => $"/export/{reviewId}/{kind.ToString().ToLowerInvariant()}.pdf";

    /// <summary>GET /export/{reviewId}/{original|corrected|annotated}.pdf — downloads a review as PDF.</summary>
    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/export/{reviewId:guid}/{kind}.pdf", async (
            Guid reviewId,
            string kind,
            IReviewRepository reviews,
            IDocumentRepository documents,
            WritingCoachService coach,
            ReviewPdfExporter exporter,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<ReviewExportKind>(kind, ignoreCase: true, out var exportKind) || !Enum.IsDefined(exportKind))
            {
                return Results.NotFound();
            }

            // Repositories only return the current user's data.
            var review = await reviews.GetAsync(reviewId, cancellationToken);
            var document = review is null ? null : await documents.GetAsync(review.DocumentId, cancellationToken);
            if (review is null || document is null)
            {
                return Results.NotFound();
            }

            var pdf = exporter.Export(document, coach.Open(review), exportKind);
            return Results.File(pdf, "application/pdf", ReviewExportFile.Name(document.Title, exportKind));
        });
    }
}
