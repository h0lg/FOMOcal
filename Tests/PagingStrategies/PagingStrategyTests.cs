using FomoCal;

namespace Tests.PagingStrategies;

public abstract class PagingStrategyTests : IDisposable
{
    private readonly MockAutomatorFactory factory = new();
    protected readonly MockScrapeLogFileSaver logFileSaver = new();
    protected readonly MockBrowser browser = new();
    protected readonly Scraper scraper;
    protected readonly Venue venue;

    public PagingStrategyTests(Venue.PagingStrategy pagingStrategy)
    {
        scraper = new(browser, factory, logFileSaver);

        venue = new()
        {
            Name = "test",
            ProgramUrl = "https://test.com/events",
            SaveScrapeLogs = true,
            Event = new()
            {
                Selector = ".event",
                Name = new ScrapeJob { Selector = "." + nameof(Event.Name) },
                Date = new DateScrapeJob { Selector = "." + nameof(Event.Date), Format = "yyyy-MM-dd" },
                PagingStrategy = pagingStrategy
            }
        };
    }

    protected static void AssertEmpty(List<Exception> errors)
        => Assert.IsEmpty(errors, errors.Select(ex => ex.ToString()).LineJoin());

    protected void AssertLogLines(params string[] expectedLines)
    {
        var actual = logFileSaver.Log;
        Assert.IsNotNull(actual);

        var actualLines = actual.Split(Environment.NewLine).ToList();
        int skip = 0;

        foreach (string expected in expectedLines)
        {
            var first = actualLines.Skip(skip).FirstOrDefault(l => l.Contains(expected));
            Assert.IsNotNull(first, $"\n\n'{expected}' was not found after skipping {skip} lines in\n\n{actual}");
            skip = actualLines.IndexOf(first, skip) + 1;
        }

        Assert.HasCount(skip, actualLines, $"log has more lines:\n{actualLines.Skip(skip).LineJoin()}");
    }

    public void Dispose() => scraper.Dispose();
}
