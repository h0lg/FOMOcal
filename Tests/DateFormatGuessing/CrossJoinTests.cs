using FomoCal;

namespace Tests.DateFormatGuessing;

[TestClass]
public class CrossJoinTests
{
    [TestMethod]
    public void CrossJoinDateFormatWithMultipleMonthAndDayOptions()
        => Test(formatOptions: [["d", "dd"], ["M", "MM"], ["yyyy"]],
            expectedFormats: [
                ["d", "M", "yyyy"],
                ["d", "MM", "yyyy"],
                ["dd", "M", "yyyy"],
                ["dd", "MM", "yyyy"]]);

    [TestMethod]
    public void CrossJoinDateFormatWithMultipleDayOptions()
        => Test(formatOptions: [["d", "dd"], ["M"], ["yyyy"]],
            expectedFormats: [
                ["d", "M", "yyyy"],
                ["dd", "M", "yyyy"]]);

    [TestMethod]
    public void CrossJoinDateFormatWithFourTokens()
        => Test(formatOptions: [["ddd", "dddd"], ["d"], ["M"], ["yyyy"]],
            expectedFormats: [
                ["ddd", "d", "M", "yyyy"],
                ["dddd", "d", "M", "yyyy"]]);

    private static void Test(string[][] formatOptions, string[][] expectedFormats)
    {
        var combinations = formatOptions.CrossJoin();
        Assert.HasCount(expectedFormats.Length, combinations);

        foreach (var expected in expectedFormats)
            combinations.AssertContainsSingle(expected);
    }
}

static partial class TestExtensions
{
    internal static void AssertContainsSingle(this IEnumerable<string[]> combinations, params string[] sequence)
        => Assert.ContainsSingle(c => c.SequenceEqual(sequence), combinations);
}
