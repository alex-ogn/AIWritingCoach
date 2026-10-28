using System.Text.RegularExpressions;

namespace AIWritingCoach.Core.Common;

public static partial class TextStats
{
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*")]
    private static partial Regex WordRegex();

    public static int CountWords(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : WordRegex().Count(text);
}
