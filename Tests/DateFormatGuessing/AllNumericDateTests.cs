using System.Globalization;
using FomoCal;

namespace Tests.DateFormatGuessing;

public abstract class DateFormatGuessingTests
{
    protected readonly static CultureInfo german = new("de"),
        usEnglish = new("en-US"), gbEnglish = new("en-Gb");

    protected static void AssertFormat(string[] inputs, CultureInfo[]? preferredCultures = null,
        string[]? expected = null, string? expectedError = null)
    {
        try
        {
            string? reportedError = null;
            var guesses = DateFormat.Guess(inputs, preferredCultures ?? [], reportError: ex => reportedError = ex);
            Assert.IsNull(reportedError);
            Assert.HasCount(expected!.Length, guesses);
            var expectedCulture = preferredCultures?.FirstOrDefault();

            foreach (var expectedFormat in expected)
                guesses.AssertContains(expectedCulture?.Name, expectedFormat);

            Assert.IsNull(expectedError);
        }
        catch (Exception ex)
        {
            // ignore warning because these asserts should only run in case of error
#pragma warning disable MSTEST0058 // Do not use asserts in catch blocks
            Assert.AreEqual(expectedError, ex.Message);
            Assert.IsNull(expected);
#pragma warning restore MSTEST0058
        }
    }
}

[TestClass]
public class AllNumericDateTests : DateFormatGuessingTests
{
    [TestMethod]
    public void German() => AssertFormat(inputs: ["13.1.2034", "9.1.2034"], expected: ["d.M.yyyy"]);

    [TestMethod]
    public void GermanWithAmbiguosMonthLength()
        => AssertFormat(inputs: ["13.1.2034"], expected: ["d.M.yyyy", "dd.M.yyyy"]);

    [TestMethod]
    public void GermanWithAmbiguousDayMonthAndTwoDigitYear()
        => AssertFormat(inputs: ["01.02.26"], expected: ["dd.MM.yy", "MM.dd.yy"]);

    [TestMethod]
    public void GermanWithAmbiguousDayMonthTwoDigitYearAndTime()
        => AssertFormat(inputs: ["01.02.26 19:00"],
            expectedError: @"Too many tokens to guess the date format from. Please select date the date cleanly, only including tokens for day, month and year:
01.02.26 19:00");

    [TestMethod]
    public void GermanWithTwoDigitYear() => AssertFormat(inputs: ["01.02.26", "13.02.26"], expected: ["dd.MM.yy"]);

    [TestMethod]
    public void GermanWithoutYear() => AssertFormat(inputs: ["13.01."], expected: ["dd.MM.", "d.MM."]);

    [TestMethod]
    public void GermanWithAmbiguosDayMonthLength()
        => AssertFormat(inputs: ["13.12.2034"], expected: ["d.M.yyyy", "d.MM.yyyy", "dd.M.yyyy", "dd.MM.yyyy"]);
}

static partial class TestExtensions
{
    internal static void AssertContains(this (string? culture, string? format)[] guesses, string? culture, string? format)
        => Assert.Contains((culture, format), guesses);
}
