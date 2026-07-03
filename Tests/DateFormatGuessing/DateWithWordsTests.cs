namespace Tests.DateFormatGuessing;

[TestClass]
public class DateWithWordsTests : DateFormatGuessingTests
{
    [TestMethod]
    public void GermanWithUnambiguosDayAndShortMonth()
        => AssertFormat(inputs: ["13 Jan 2034"],
            preferredCultures: [german],
            expected: ["d MMM yyyy", "dd MMM yyyy"]);

    [TestMethod]
    public void GermanWithShortMonthAndNoSpaces()
        => AssertFormat(inputs: ["01Jan2034"],
            preferredCultures: [german],
            expected: ["ddMMMyyyy"]);

    [TestMethod]
    public void GermanWithUnambiguosDayAndLongMonth()
        => AssertFormat(inputs: ["13 Dezember 2034"],
            preferredCultures: [german],
            expected: ["d MMMM yyyy", "dd MMMM yyyy"]);

    [TestMethod]
    public void GermanWithUnambiguosDayShortWeekDayAndMonth()
        => AssertFormat(inputs: ["So. 22 Feb 2026"],
            preferredCultures: [german],
            expected: ["ddd. dd MMM yyyy", "ddd. d MMM yyyy"]);

    [TestMethod]
    public void GermanWithDayShortWeekDayShortMonthAndTime()
        => AssertFormat(inputs: ["Do29Okt20:00"],
            preferredCultures: [german],
            expectedError: @"Too many tokens to guess the date format from. Please select date the date cleanly, only including tokens for day, month and year:
Do29Okt20:00");

    [TestMethod]
    public void GermanWithShortWeekDayAndMonth()
        => AssertFormat(inputs: ["So. 22 Feb 2026", "Do. 5 Feb 2026"],
            preferredCultures: [german],
            expected: ["ddd. d MMM yyyy"]);

    [TestMethod]
    public void GermanWithAmbiguosDayAndShortWeekDay()
        => AssertFormat(inputs: ["So 22.02.2026"],
            preferredCultures: [german],
            expected: ["ddd d.MM.yyyy", "ddd dd.MM.yyyy"]);

    [TestMethod]
    public void GermanWithoutYear()
        => AssertFormat(inputs: ["So, 22 Feb"],
            preferredCultures: [german],
            expected: ["ddd, d MMM", "ddd, dd MMM"]);

    [TestMethod]
    public void GermanWithTwoDigitYear()
        => AssertFormat(inputs: ["Do 05.02.26"],
            preferredCultures: [german],
            expected: ["ddd dd.MM.yy"]);

    [TestMethod]
    public void UsEnglishWithTwoDigitYear()
        => AssertFormat(inputs: ["Thu 02/05/26"],
            preferredCultures: [usEnglish],
            expected: ["ddd MM/dd/yy"]);

    [TestMethod]
    public void BritishEnglishWithTwoDigitYear()
        => AssertFormat(inputs: ["Thu 05/02/26"],
            preferredCultures: [gbEnglish],
            expected: ["ddd dd/MM/yy"]);

    [TestMethod]
    public void BritishEnglishWithoutYear()
        => AssertFormat(inputs: ["Thu 05 Feb"],
            preferredCultures: [gbEnglish],
            expected: ["ddd dd MMM"]);
}
