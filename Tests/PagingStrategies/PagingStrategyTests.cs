using FomoCal;

namespace Tests.PagingStrategies;

public abstract class PagingStrategyTests : IDisposable
{
    protected readonly MockScrapeLogFileSaver logFileSaver = new();
    protected readonly MockBrowser browser = new();
    private readonly MockAutomatorFactory factory;
    protected readonly Scraper scraper;
    protected readonly Venue venue;

    protected PagingStrategyTests(Venue.PagingStrategy pagingStrategy)
    {
        factory = new(browser);
        scraper = new(browser, factory, logFileSaver);

        venue = new()
        {
            Name = "test",
            ProgramUrl = "https://test.com/events",
            SaveScrapeLogs = true,
            Event = new()
            {
                Selector = ".event",
                Filter = "concert",
                Name = new ScrapeJob { Selector = "." + nameof(Event.Name) },
                Date = new DateScrapeJob { Selector = "." + nameof(Event.Date), Format = "yyyy-MM-dd" },
                NextPageSelector = ".next-page",
                PagingStrategy = pagingStrategy
            }
        };
    }

    protected static void AssertEmpty(List<Exception> errors)
        => Assert.IsEmpty(errors, errors.Select(ex => ex.ToString()).LineJoin());

    private const string unexpectedLinePrefix = "NOT! ";

    protected void AssertLogLines(params string[] expectedLines)
    {
        var actual = logFileSaver.Log;
        Assert.IsNotNull(actual);

        var actualLines = actual.Split(Environment.NewLine).ToList();
        int skipped = 0;

        foreach (string line in expectedLines)
        {
            var isUnexpected = line.StartsWith(unexpectedLinePrefix);
            var expected = isUnexpected ? line[unexpectedLinePrefix.Length..] : line;
            var firstMatch = actualLines.Skip(skipped).FirstOrDefault(l => l.Contains(expected));
            string error = $"\n\n'{expected}' was {(isUnexpected ? null : "not ")}found after skipping {skipped} lines in\n\n{actual}";

            if (isUnexpected) Assert.IsNull(firstMatch, error); // assert expected is not contained in any line after skipped
            else // make sure expected line is found and update skipped to skip it on next iteration
            {
                Assert.IsNotNull(firstMatch, error);
                skipped = actualLines.IndexOf(firstMatch, skipped) + 1;
            }
        }

        Assert.HasCount(skipped, actualLines, $"log has more lines:\n{actualLines.Skip(skipped).LineJoin()}");
    }

    protected static string HasNoMore(string unexpectedLine) => unexpectedLinePrefix + unexpectedLine;

    public void Dispose() => scraper.Dispose();
}

internal static class TestExtensions
{
    internal static bool TryGetAt<T>(this List<T> list, uint index, out T value)
    {
        if (index < list.Count)
        {
            value = list[(int)index];
            return true;
        }

        value = default!;
        return false;
    }
}
