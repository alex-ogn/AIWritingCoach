using AIWritingCoach.Core.Diff;
using AIWritingCoach.Core.Reviews;
using Microsoft.AspNetCore.Components;

namespace AIWritingCoach.Web.Components.Writing;

/// <summary>Shared parameters and styling helpers of the inline and side-by-side diff views.</summary>
public abstract class DiffComponentBase : ComponentBase
{
    [Parameter, EditorRequired]
    public DiffDocument Diff { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<ReviewError> Errors { get; set; } = [];

    [Parameter]
    public int? SelectedHunk { get; set; }

    [Parameter]
    public EventCallback<DiffHunk> OnSelect { get; set; }

    public static string HunkElementId(DiffHunk hunk) => $"hunk-{hunk.Index}";

    protected string HunkClass(DiffHunk hunk, string? extra = null) => string.Join(' ', new[]
    {
        "hunk",
        hunk.Accepted ? "hunk-accepted" : "hunk-rejected",
        hunk.ErrorIndexes.Count == 0 ? "hunk-unexplained" : null,
        SelectedHunk == hunk.Index ? "hunk-selected" : null,
        extra,
    }.Where(c => c is not null));

    protected string Tooltip(DiffHunk hunk) => string.Join("\n", hunk.ErrorIndexes
        .Select(i => Errors.FirstOrDefault(e => e.Index == i))
        .Where(e => e is not null)
        .Select(e => $"{e!.Category}: {e.Explanation}"));
}
