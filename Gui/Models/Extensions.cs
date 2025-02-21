namespace FomoCal;

internal static class StringExtensions
{
    internal static bool HasSignificantValue(this string? str) => !string.IsNullOrWhiteSpace(str);
}
