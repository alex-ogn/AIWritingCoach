using AIWritingCoach.Core.Diff;
using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Infrastructure.Diff;

namespace AIWritingCoach.Tests.Diff;

public class DiffPlexTextDiffServiceTests
{
    private readonly DiffPlexTextDiffService _service = new();

    [Theory]
    [InlineData("")]
    [InlineData("Hello, world!")]
    [InlineData("I don't think it's well-known.\r\n\nSecond  paragraph\twith tabs… and ünïcödé")]
    public void Tokenize_concatenates_back_to_input(string text)
    {
        Assert.Equal(text, string.Concat(DiffPlexTextDiffService.Tokenize(text)));
    }

    [Fact]
    public void Single_word_replacement_produces_one_hunk_with_offsets()
    {
        var diff = _service.Compute("Yesterday I goed home.", "Yesterday I went home.", []);

        var hunk = Assert.Single(diff.Hunks);
        Assert.Equal(DiffHunkKind.Replace, hunk.Kind);
        Assert.Equal("goed", hunk.OriginalText);
        Assert.Equal("went", hunk.CorrectedText);
        Assert.Equal("Yesterday I ".Length, hunk.OriginalStart);
        Assert.Equal("Yesterday I ".Length, hunk.CorrectedStart);
    }

    [Theory]
    [InlineData("She go to school every days.", "She goes to school every day.")]
    [InlineData("This is more better than that", "This is better than that.")]
    [InlineData("Dear Sir,\n\nI writing to complain.\nRegards", "Dear Sir or Madam,\n\nI am writing to complain.\n\nKind regards,")]
    [InlineData("Same text.", "Same text.")]
    [InlineData("", "Completely new.")]
    public void Accept_all_yields_corrected_and_reject_all_yields_original(string original, string corrected)
    {
        var diff = _service.Compute(original, corrected, []);

        diff.SetAll(true);
        Assert.Equal(corrected, diff.BuildText());

        diff.SetAll(false);
        Assert.Equal(original, diff.BuildText());
    }

    [Fact]
    public void Rejecting_one_hunk_keeps_only_that_original()
    {
        var diff = _service.Compute("She go to school every days.", "She goes to school every day.", []);
        Assert.Equal(2, diff.Hunks.Count);

        diff.ApplyRejections([diff.Hunks[1].Index]);

        Assert.Equal("She goes to school every days.", diff.BuildText());
        Assert.Equal([1], diff.RejectedIndexes);
    }

    [Fact]
    public void Adjacent_word_changes_separated_by_a_space_are_merged()
    {
        var diff = _service.Compute("It was very very good", "It was extremely nice", []);

        var hunk = Assert.Single(diff.Hunks);
        Assert.Equal("very very good", hunk.OriginalText);
        Assert.Equal("extremely nice", hunk.CorrectedText);
    }

    [Fact]
    public void Errors_are_linked_to_the_hunks_they_explain()
    {
        var errors = new List<ReviewError>
        {
            new() { Index = 0, Original = "go", Correction = "goes", Category = "Grammar" },
            new() { Index = 1, Original = "days", Correction = "day", Category = "Grammar" },
            new() { Index = 2, Original = "nonexistent", Correction = "x", Category = "Other" },
        };

        var diff = _service.Compute("She go to school every days.", "She goes to school every day.", errors);

        Assert.Equal([0], diff.Hunks[0].ErrorIndexes);
        Assert.Equal([1], diff.Hunks[1].ErrorIndexes);
        Assert.Empty(diff.HunksForError(2));
    }

    [Fact]
    public void Repeated_words_link_to_the_occurrence_that_changed()
    {
        var errors = new List<ReviewError> { new() { Index = 0, Original = "the", Correction = "a", Category = "Articles" } };

        var diff = _service.Compute("I saw the cat and the dog.", "I saw the cat and a dog.", errors);

        var hunk = Assert.Single(diff.Hunks);
        Assert.Equal([0], hunk.ErrorIndexes);
        Assert.Equal("I saw the cat and ".Length, hunk.OriginalStart);
    }

    [Fact]
    public void Pure_insertion_is_linked_through_the_correction()
    {
        var errors = new List<ReviewError> { new() { Index = 0, Original = "", Correction = "am", Category = "Grammar" } };

        var diff = _service.Compute("I writing.", "I am writing.", errors);

        var hunk = Assert.Single(diff.Hunks);
        Assert.Equal(DiffHunkKind.Insert, hunk.Kind);
        Assert.Equal([0], hunk.ErrorIndexes);
    }

    [Fact]
    public void Side_by_side_rows_align_on_unchanged_line_breaks()
    {
        var diff = _service.Compute("Line one.\nLine two is bad.\nLine three.", "Line one.\nLine two is good.\nLine three.", []);

        var rows = SideBySideLayout.Build(diff);

        Assert.Equal(3, rows.Count);
        Assert.False(rows[0].HasChanges);
        Assert.True(rows[1].HasChanges);
        Assert.Equal("Line two is bad.", string.Concat(rows[1].Left.Select(p => p.Text)));
        Assert.Equal("Line two is good.", string.Concat(rows[1].Right.Select(p => p.Text)));
    }
}
