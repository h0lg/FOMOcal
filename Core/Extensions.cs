using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FomoCal;

public static partial class StringExtensions
{
    public static bool IsNullOrWhiteSpace(this string? str) => string.IsNullOrWhiteSpace(str);
    public static bool IsSignificant(this string? str) => !string.IsNullOrWhiteSpace(str);
    public static string Join(this IEnumerable<string?> strings, string separator) => string.Join(separator, strings);
    public static string GetLast(this string input, int number) => input.Length > number ? input[^number..] : input;
    public static string LineJoin(this IEnumerable<string?> strings) => strings.Join(Environment.NewLine);

    /// <summary>Indicates whether <paramref name="text"/> contains any of the supplied
    /// <paramref name="terms"/> using <paramref name="stringComparison"/> to compare.</summary>
    public static bool ContainsAny(this string text, IEnumerable<string> terms,
        StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase)
        => terms.Any(t => text.Contains(t, stringComparison));

    private static readonly char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

    internal static string MakeFileNameSafe(this string name, char replacement = '_')
        => string.Concat(name.Select(c => invalidFileNameChars.Contains(c) ? replacement : c));

    internal static string CsvEscape(this string? value)
        => value.IsNullOrWhiteSpace() ? "" : $"\"{value!.Replace("\"", "\"\"")}\"";

    [GeneratedRegex(@"\s+")] private static partial Regex ConsecutiveWhitespace();

    public static string NormalizeWhitespace(this string? input)
    {
        if (input.IsNullOrWhiteSpace()) return string.Empty;
        string result = ConsecutiveWhitespace().Replace(input!, " "); // replace with single space
        return result.Trim(); // trim leading/trailing whitespace
    }

    internal static string ApplyReplacements(this string input, Dictionary<string, string> replacements)
    {
        foreach (var pair in replacements) // Apply each replacement pair
            input = Regex.Replace(input, pair.Key, pair.Value);

        return input;
    }

    /// <summary>Explodes the in-lined <paramref name="replacements"/> in the form "Pattern }} Replacement || Pattern2 }}"
    /// into pairs for <see cref="ApplyReplacements(string, Dictionary{string, string})"/>.</summary>
    public static Dictionary<string, string> ExplodeInlinedReplacements(this string replacements)
        => replacements!.Split("||", StringSplitOptions.RemoveEmptyEntries)
            .Select(replacement => replacement.Split("}}"))
            .ToDictionary(arr => arr[0].Trim(), arr => arr[1].Trim());
}

public static class EnumerableExtensions
{
    /// <summary>Returns only the non-null elements from <paramref name="nullables"/>.</summary>
    public static IEnumerable<T> WithValue<T>(this IEnumerable<T?> nullables)
        => nullables.Where(v => v != null).Select(v => v!);

    public static IEnumerable<T[]> CrossJoin<T>(this IEnumerable<IEnumerable<T>> sequences)
    {
        IEnumerable<T[]> result = [[]];

        foreach (var sequence in sequences)
        {
            result =
                from acc in result
                from item in sequence
                select acc.Append(item).ToArray();
        }

        return result;
    }

    public static IEnumerable<(T1, T2)> CrossJoinWith<T1, T2>(this IEnumerable<T1> first, IEnumerable<T2> second)
        => first.SelectMany(f => second.Select(s => (f, s)));
}

public static class EnumExtensions
{
    public static string GetDescription<T>(this T value) where T : Enum
    {
        var field = typeof(T).GetField(value.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
}
