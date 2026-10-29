using System.Text;

namespace AIWritingCoach.Core.Reviews;

public enum ReviewExportKind
{
    /// <summary>The text exactly as it was submitted for the check.</summary>
    Original,

    /// <summary>The AI-corrected text with the user's accept/reject decisions applied.</summary>
    Corrected,

    /// <summary>The submitted text with every change marked, plus feedback, scores, mistakes and vocabulary.</summary>
    Annotated,
}

public static class ReviewExportFile
{
    private const int MaxTitleLength = 60;

    /// <summary>A safe download name such as <c>My essay - corrected.pdf</c>.</summary>
    public static string Name(string? title, ReviewExportKind kind)
    {
        var builder = new StringBuilder();
        foreach (var c in (title ?? string.Empty).Trim())
        {
            builder.Append(char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : ' ');
        }

        var name = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length > MaxTitleLength)
        {
            name = name[..MaxTitleLength].TrimEnd();
        }

        var suffix = kind.ToString().ToLowerInvariant();
        return name.Length == 0 ? $"text - {suffix}.pdf" : $"{name} - {suffix}.pdf";
    }
}
