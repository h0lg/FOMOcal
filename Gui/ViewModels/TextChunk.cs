using System.Globalization;
using CommunityToolkit.Maui.Markup;

namespace FomoCal.Gui.ViewModels;

/// <summary>Represents a piece of text separated from its siblings for the purpose of displaying it in a particular style.
/// A collection of these represents the full text and can be cached - so that the chunking/slicing only has to happen once
/// and rebuilding the UI using the <see cref="TextChunkConverter"/> becomes cheaper.</summary>
public sealed record TextChunk
{
    /// <summary>The text to render.</summary>
    public required string Text { get; init; }

    /// <summary>Whether <see cref="Text"/> matches the search terms
    /// passed to <see cref="TextChunkExtensions.ChunkBy(string?, string[])"/>.</summary>
    public bool Matches { get; init; }

    /// <summary>The URL that <see cref="Text"/> should link to.</summary>
    public string? LinkUrl { get; init; }
}

internal static class TextChunkExtensions
{
    // Collects all raw matches (may overlap)
    private static List<(int index, int length)> GetMatches(this string text, string[] searchTerms)
    {
        var raw = new List<(int index, int length)>();

        foreach (var term in searchTerms)
        {
            if (string.IsNullOrWhiteSpace(term))
                continue;

            int index = 0;

            while (true)
            {
                index = text.IndexOf(term, startIndex: index, StringComparison.OrdinalIgnoreCase);
                if (index == -1) break;
                raw.Add((index, term.Length));
                index += term.Length;
            }
        }

        return raw;
    }

    private static IEnumerable<(int index, int length)> MergeOverlaps(this List<(int index, int length)> rawMatches)
    {
        if (rawMatches.Count == 0) yield break;

        // Sort by starting position
        rawMatches.Sort((a, b) => a.index.CompareTo(b.index));

        // Merge overlaps
        int currentStart = rawMatches[0].index;
        int currentEnd = rawMatches[0].index + rawMatches[0].length;

        for (int i = 1; i < rawMatches.Count; i++)
        {
            var (nextStart, nextLength) = rawMatches[i];
            int nextEnd = nextStart + nextLength;

            if (nextStart <= currentEnd)
            {
                // Overlapping or adjacent, extend the interval
                if (currentEnd < nextEnd) currentEnd = nextEnd;
            }
            else
            {
                // Output current and start new interval
                yield return (currentStart, currentEnd - currentStart);

                currentStart = nextStart;
                currentEnd = nextEnd;
            }
        }

        // Emit last merged match
        yield return (currentStart, currentEnd - currentStart);
    }

    internal static List<TextChunk>? ChunkBy(this string? text, string[] searchTerms)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (searchTerms.Length == 0) return [new() { Text = text }];
        var chunks = new List<TextChunk>();
        int index = 0;

        foreach (var (start, length) in text.GetMatches(searchTerms).MergeOverlaps())
        {
            if (start > index)
                chunks.Add(new TextChunk { Text = text[index..start] });

            chunks.Add(new TextChunk
            {
                Text = text.Substring(start, length),
                Matches = true
            });

            index = start + length;
        }

        if (index < text.Length) chunks.Add(new TextChunk { Text = text[index..] });
        return chunks;
    }

    internal static IEnumerable<TextChunk> ChunkByLinksAnd(this string? text, string[] searchTerms)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        foreach ((string display, string? url) in text.ChunkByLinksAndUrls())
        {
            if (url == null)
            {
                if (string.IsNullOrWhiteSpace(display)) continue;

                foreach (var chunk in display.ChunkBy(searchTerms)!)
                    yield return chunk;
            }
            else yield return new() { Text = display, LinkUrl = url }; // link
        }
    }

    internal static List<TextChunk>? PrependWith(this List<TextChunk>? chunks, string label)
    {
        if (chunks == null || chunks.Count == 0) return null;
        chunks.Insert(0, new() { Text = label });
        return chunks;
    }
}

public class TextChunkConverter(Style linkStyle, Style highlitStyle, Style? normalStyle = null) : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IReadOnlyList<TextChunk> chunks || chunks.Count == 0) return null;

        var fs = new FormattedString();

        foreach (var chunk in chunks)
        {
            Span span = new() { Text = chunk.Text };

            if (chunk.LinkUrl == null) span.Style = chunk.Matches ? highlitStyle : normalStyle;
            else
            {
                span.Style = linkStyle;
                span.TapGesture(() => Launcher.OpenAsync(new Uri(chunk.LinkUrl)));
            }

            fs.Spans.Add(span);
        }

        return fs;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
