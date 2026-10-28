using System.Globalization;
using AIWritingCoach.Core.Diff;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Localization;

namespace AIWritingCoach.Web.Components.Writing;

/// <summary>Two-pane, line-aligned diff (WinMerge style): original on the left, corrected on the right.</summary>
public sealed class SideBySideDiff : DiffComponentBase
{
    private DiffDocument? _layoutFor;
    private IReadOnlyList<SideBySideRow> _rows = [];

    [Inject]
    private IStringLocalizer<SharedResource> L { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_layoutFor, Diff))
        {
            _rows = SideBySideLayout.Build(Diff);
            _layoutFor = Diff;
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "diff diff-sbs");

        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "sbs-row sbs-header");
        Cell(builder, "sbs-num", string.Empty);
        Cell(builder, "sbs-cell", L["Diff.Original"]);
        Cell(builder, "sbs-num", string.Empty);
        Cell(builder, "sbs-cell", L["Diff.Corrected"]);
        builder.CloseElement();

        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var lineNumber = (i + 1).ToString(CultureInfo.InvariantCulture);

            builder.OpenElement(4, "div");
            builder.SetKey(i);
            builder.AddAttribute(5, "class", row.HasChanges ? "sbs-row sbs-changed" : "sbs-row");
            Cell(builder, "sbs-num", lineNumber);
            Pieces(builder, row.Left, isLeft: true);
            Cell(builder, "sbs-num", lineNumber);
            Pieces(builder, row.Right, isLeft: false);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void Pieces(RenderTreeBuilder builder, List<SidePiece> pieces, bool isLeft)
    {
        builder.OpenRegion(10);
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", isLeft ? "sbs-cell sbs-left" : "sbs-cell sbs-right");

        foreach (var piece in pieces)
        {
            if (piece.Hunk is not { } hunk)
            {
                builder.AddContent(2, piece.Text);
                continue;
            }

            var kind = piece.Text.Length == 0 ? "gap" : isLeft ? "del" : "ins";
            builder.OpenElement(3, "span");
            if (!isLeft)
            {
                builder.AddAttribute(4, "id", HunkElementId(hunk));
            }

            builder.AddAttribute(5, "class", HunkClass(hunk, kind));
            builder.AddAttribute(6, "title", Tooltip(hunk));
            builder.AddAttribute(7, "onclick", EventCallback.Factory.Create(this, () => OnSelect.InvokeAsync(hunk)));
            builder.AddContent(8, piece.Text);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseRegion();
    }

    private static void Cell(RenderTreeBuilder builder, string cssClass, string text)
    {
        builder.OpenRegion(20);
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", cssClass);
        builder.AddContent(2, text);
        builder.CloseElement();
        builder.CloseRegion();
    }
}
