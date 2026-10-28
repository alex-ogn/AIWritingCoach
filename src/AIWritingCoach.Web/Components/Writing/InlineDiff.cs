using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AIWritingCoach.Web.Components.Writing;

/// <summary>
/// Single-pane diff: deletions struck through, insertions underlined, in reading order.
/// Rendered in C# so no Razor whitespace leaks into the <c>pre-wrap</c> text.
/// </summary>
public sealed class InlineDiff : DiffComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "diff diff-inline");

        foreach (var segment in Diff.Segments)
        {
            if (segment.Hunk is not { } hunk)
            {
                builder.AddContent(2, segment.Text);
                continue;
            }

            builder.OpenElement(3, "span");
            builder.SetKey(hunk.Index);
            builder.AddAttribute(4, "id", HunkElementId(hunk));
            builder.AddAttribute(5, "class", HunkClass(hunk));
            builder.AddAttribute(6, "title", Tooltip(hunk));
            builder.AddAttribute(7, "onclick", EventCallback.Factory.Create(this, () => OnSelect.InvokeAsync(hunk)));

            if (hunk.OriginalText.Length > 0)
            {
                builder.OpenElement(8, "del");
                builder.AddContent(9, hunk.OriginalText);
                builder.CloseElement();
            }

            if (hunk.CorrectedText.Length > 0)
            {
                builder.OpenElement(10, "ins");
                builder.AddContent(11, hunk.CorrectedText);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
