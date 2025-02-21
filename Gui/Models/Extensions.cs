namespace FomoCal;

internal static class StringExtensions
{
    internal static bool IsSignificant(this string? str) => !string.IsNullOrWhiteSpace(str);
}
