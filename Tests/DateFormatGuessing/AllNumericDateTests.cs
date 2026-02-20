using System.Globalization;
using FomoCal;

namespace Tests.DateFormatGuessing;

public abstract class DateFormatGuessingTests
{
    protected readonly static CultureInfo german = new("de"),
        usEnglish = new("en-US"), gbEnglish = new("en-Gb");

    protected static void AssertFormat(string[] inputs, CultureInfo[]? preferredCultures = null, string[] expected)
    {
        var guesses = DateFormat.Guess(inputs, preferredCultures ?? []);
        Assert.HasCount(expected.Length, guesses);
        var expectedCulture = preferredCultures?.FirstOrDefault();

        foreach (var expectedFormat in expected)
            guesses.AssertContains(expectedCulture?.Name, expectedFormat);
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
