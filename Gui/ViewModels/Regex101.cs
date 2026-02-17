using CommunityToolkit.Maui.Markup;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

internal static class Regex101
{
    private const string BaseUrl = "https://regex101.com/";

    /// <summary>Builds a regex101 deep link for .NET flavor with default .NET Regex options.</summary>
    /// <param name="pattern">The regex pattern.</param>
    /// <param name="testString">The test input string.</param>
    /// <returns>A fully formed regex101 URL.</returns>
    private static Dictionary<string, string?> BuildDeepLinkParameters(string pattern, string testString, string? replacement)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(testString);

        const string flags = "g"; // for multi-line replacements

        Dictionary<string, string?> parameters = new()
        {
            {"regex", pattern},
            {"testString", testString},
            {"flags", flags},
            {"flavor", "dotnet"},
        };

        if (replacement != null) parameters.Add("subst", replacement);
        return parameters;
    }

    internal static Border DeepLink(ScrapeJob.Step step, InputView input,
        Func<ScrapeJob.Step?, IEnumerable<string>> getPreviewValuesBeforeStep)
    {
        var btn = new Border
        {
            Content = Lbl(Glyphs.Test).StyleClass(Styles.Label.EndingEntryButton),
            StyleClass = [Styles.Border.EndingEntryButton],
            IsVisible = false,
            IsEnabled = false
        }.ToolTip(Glyphs.Test + " test this RegEx on " + BaseUrl);

        // toggle button enabled with input having a value
        input.TextChanged += (o, e) => btn.IsVisible = btn.IsEnabled = e.NewTextValue.IsSignificant();

        btn.TapGesture(async () =>
        {
            if (input.Text.IsNullOrWhiteSpace()) return;

            if (!App.HasInternet)
            {
                await App.CurrentPage.DisplayAlertAsync("Connect to the internet and retry.",
                    $"Testing RegEx on {BaseUrl} is an online feature.", "OK");

                return;
            }

            string pattern = input.Text;
            string? replacement = null;

            if (step == ScrapeJob.Step.Replacements)
            {
                var replacements = pattern.ExplodeInlinedReplacements();

                if (replacements.Count > 1)
                {
                    const string cancel = "none";

                    var choice = await App.CurrentPage.DisplayActionSheetAsync(
                        "Which pattern do you want to test?", cancel, null, [.. replacements.Keys]);

                    if (choice == null || choice == cancel) return;
                    pattern = choice;
                }
                else pattern = replacements.Keys.FirstOrDefault() ?? pattern;

                if (replacements.TryGetValue(pattern, out var rplcmnt))
                    replacement = rplcmnt;
            }

            var testString = getPreviewValuesBeforeStep(step).LineJoin();
            var parameters = BuildDeepLinkParameters(pattern, testString, replacement);
            await WebViewPage.OpenUrlAsync(BaseUrl, parameters);
        });

        return btn;
    }
}
