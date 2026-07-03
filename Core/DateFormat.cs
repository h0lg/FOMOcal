using System.Globalization;
using System.Text.RegularExpressions;

namespace FomoCal;

public static partial class DateFormat
{
    private static readonly string[] weekDays = ["ddd", "dddd"];

    public static (string? culture, string? format)[] Guess(string[] inputs, CultureInfo[] preferredCultures, Action<string> reportError)
    {
        if (inputs.Length == 0) return [];
        (string? culture, string format)[]? formats = null;
        Exception? inputError = null;

        try { formats = TokenizedGuess(inputs, preferredCultures); }
        catch (Exception ex)
        {
            if (ex is ArgumentException inpex)
                inputError = inpex;  // remember input error
            else
            {
                var inpts = inputs.Prepend("inputs:").LineJoin();
                var cultrs = preferredCultures.Select(c => c.ToString()).Prepend("preferred date cultures:").LineJoin();
                reportError($"{inputs}\n\n{cultrs}\n\n{ex}");
            }
        }

        if (formats?.Length > 0) return [.. formats];

        // fall-back to guessing from preferredDateCultures
        var found = TryParseWithCultures(inputs, preferredCultures);

        if (found.Any()) return [.. found];
        if (inputError == null) return [];
        throw inputError;
    }

    private static (string input, List<int> numbers, MatchCollection matches) FindInputWithUnambiguousNumbers(string[] inputs)
    {
        foreach (var input in inputs)
        {
            var (numberMatches, numbers, isUnambiguous) = AnalyzeInput(input);
            if (isUnambiguous) return (input, numbers, numberMatches);
        }

        return default;
    }

    private static (MatchCollection numberMatches, List<int> numbers, bool isUnambiguous) AnalyzeInput(string input)
    {
        var numberMatches = Numbers().Matches(input);
        List<int> numbers = [.. numberMatches.Select(match => int.Parse(match.Value))];
        var isUnambiguous = numbers.Count(MustBeYear) == 1 && numbers.Count(MustBeDayOrYear) == 2;
        return (numberMatches, numbers, isUnambiguous);
    }

    private static (string? culture, string format)[] TokenizedGuess(string[] inputs, CultureInfo[] preferredCultures)
    {
        var input = inputs[0];
        var (numberMatches, numbers, isUnambiguous) = AnalyzeInput(input);

        if (!isUnambiguous)
        {
            var found = FindInputWithUnambiguousNumbers(inputs);

            if (found != default)
            {
                input = found.input;
                numbers = found.numbers;
                numberMatches = found.matches;
                isUnambiguous = true;
            }
        }

        var year = numbers.SingleOrDefault(MustBeYear);

        if (year == 0) // none of the numbers is unambiguosly a year
        {
            // if there are 3 numbers, assume the year is the last
            if (numberMatches.Count == 3) year = numbers[2];
        }

        var yearToken = year == 0 ? null : numberMatches[numbers.IndexOf(year)].Value;
        var yearFormat = yearToken == null ? null : new string([.. Enumerable.Repeat('y', yearToken.Length)]);
        var notMonth = numbers.Where(n => n != year && MustBeDayOrYear(n)).ToArray();

        if (notMonth.Length > 1) throw new ArgumentException(
            inputs.Prepend("Too many tokens to guess the date format from. Please select date the date cleanly, only including tokens for day, month and year:").LineJoin());

        var day = notMonth.Length == 0 ? 0 : notMonth[0];
        var dayTokenMaybeMonth = false;

        if (day == 0)
        {
            var candidates = numbers.Where(n => n != year).ToArray();

            if (candidates.Length == 1)
                day = candidates[0];
            else if (candidates.Length == 2)
            {
                day = candidates[0];
                dayTokenMaybeMonth = true;
            }
        }

        var dayToken = numberMatches[numbers.IndexOf(day)].Value;
        const string shortDay = "d", longDay = "dd";

        string[] dayFormats = dayToken.Length == 1 ? [shortDay]
            : dayToken.StartsWith('0') ? [longDay]
            : [shortDay, longDay];

        string monthToken;
        string[] monthFormats;
        string? weekDayToken;
        string[] weekDayFormats = [];
        var letterMatches = Letters().Matches(input);

        if (numberMatches.Count == 3 || letterMatches.Count == 0)
        {
            var month = numbers.Single(n => n != year && n != day);
            monthToken = numberMatches[numbers.IndexOf(month)].Value;
            const string oneDigitMonth = "M", twoDigitMonth = "MM";

            monthFormats = monthToken.Length == 1 ? [oneDigitMonth]
                : monthToken.StartsWith('0') ? [twoDigitMonth]
                : [oneDigitMonth, twoDigitMonth];

            if (letterMatches.Count == 1)
            {
                weekDayToken = letterMatches[0].Value;
                weekDayFormats = weekDays;
            }
        }
        else
        {
            string[] monthNames = ["MMM", "MMMM"];

            if (letterMatches.Count == 1)
            {
                monthToken = letterMatches[0].Value;
                monthFormats = monthNames;
            }
            else if (letterMatches.Count == 2)
            {
                // assume the other token is the week day
                weekDayToken = letterMatches[0].Value;
                monthToken = letterMatches[1].Value;

                // use the same options for both tokens because guessing which is which is hard
                weekDayFormats = monthFormats = [.. monthNames, .. weekDays];
            }
            else
            {
                return [];
            }
        }

        Match[] matches = letterMatches == null ? [.. numberMatches.AsEnumerable()]
            : [.. numberMatches.Concat(letterMatches).OrderBy(m => m.Index)];

        if (dayTokenMaybeMonth) // try day formats also where month token is and vice versa
        {
            var df = dayFormats; // remember unmodified dayFormats
            dayFormats = [.. dayFormats, .. monthFormats];
            monthFormats = [.. df, .. monthFormats];
        }

        var combinations = matches // must be in the order they occur
            .Select(match => // map match to format options
                match.Value == yearToken ? [yearFormat!]
                    : match.Value == monthToken ? monthFormats
                    : match.Value == dayToken ? dayFormats
                    : weekDayFormats)
            .CrossJoin() // form cartesian product to get all possible format combinations
            /* Only continue with combinations with unique formats.
             * For more than one letter match, we cross-join the same format options for month and week day,
             * leading to invalid combinations with the same format option used for both tokens. */
            .Where(combo => combo.Distinct().Count() == combo.Length)
            .Select(combination => // replace tokens in input with their formats
            {
                var pattern = input; // to keep original input in tact
                int lengthDiff = 0;

                for (int i = 0; i < combination.Length; i++)
                {
                    var match = matches[i];
                    var format = combination[i];
                    int index = match.Index + lengthDiff; // correct for previous replacements with length diffs between token and format
                    pattern = string.Concat(pattern.AsSpan(0, index), format, pattern.AsSpan(index + match.Length));
                    lengthDiff += (format.Length - match.Length); // accumulate length diff to accurately replace following tokens
                }

                return pattern;
            })
            .ToArray();

        var cultures = preferredCultures.Length == 0 ? [CultureInfo.InvariantCulture] : preferredCultures;

        var guesses = cultures.CrossJoinWith(combinations)
            .Where(pair => MatchesAll(format: pair.Item2, inputs, culture: pair.Item1))
            .Select(pair => (pair.Item1.Name.IsNullOrWhiteSpace() ? null : pair.Item1.Name, pair.Item2))
            .ToArray();

        return [.. guesses];
    }

    private static bool MatchesAll(string format, string[] inputs, CultureInfo? culture)
        => inputs.All(input => DateTime.TryParseExact(input, format,
            culture ?? CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var _));

    private static bool MustBeYear(int number) => number > 31; // not day or month
    private static bool MustBeDayOrYear(int number) => number > 12; // not month

    private static IEnumerable<(string culture, string? format)> TryParseWithCultures(string[] inputs, IEnumerable<CultureInfo> cultures)
    {
        foreach (var culture in cultures)
        {
            var longDates = culture.DateTimeFormat.GetAllDateTimePatterns('D');
            var shortDates = culture.DateTimeFormat.GetAllDateTimePatterns('d');
            var monthDays = culture.DateTimeFormat.GetAllDateTimePatterns('m');
            string[] longDatesWithoutWeekDay = [.. longDates.Where(pattern => !pattern.ContainsAny(weekDays))];

            string[] patterns = [.. longDates, .. shortDates, .. monthDays,
                .. GetCombinedPatterns(weekDays, ", ", shortDates),
                .. GetCombinedPatterns(weekDays, ", ", longDatesWithoutWeekDay)];

            foreach (var pattern in patterns)
                if (inputs.All(input => DateTime.TryParseExact(input, pattern, culture, DateTimeStyles.AllowWhiteSpaces, out var result)))
                    yield return (culture.Name, pattern);

            if (inputs.All(input => DateTime.TryParse(input, culture, out var fallback)))
                yield return (culture.Name, null);
        }
    }

    private static string[] GetCombinedPatterns(string[] patterns1, string glue, string[] patterns2)
        => [.. patterns1.CrossJoinWith(patterns2).Select(pair => pair.Item1 + glue + pair.Item2)];

    [GeneratedRegex(@"\d+")] private static partial Regex Numbers();
    [GeneratedRegex(@"\p{L}+")] private static partial Regex Letters();
}
