using System.Reflection;

namespace FomoCal;

internal static class ExportSettings
{
    private const string textAlignedWithHeadersPreferencesKey = "Export.TextAlignedWithHeaders";
    private static readonly RememberedStrings htmlEventFields = new("Export.HtmlEventFields");
    private static readonly RememberedStrings textEventFields = new("Export.TextEventFields");

    internal static IEnumerable<PropertyInfo> EventFieldsForHtml
    {
        get => LoadEventProperties(htmlEventFields, () => [.. Event.Fields.Select(p => p.Name)]);
        set => SaveEventProperties(value, htmlEventFields);
    }

    internal static IEnumerable<PropertyInfo> EventFieldsForText
    {
        get => LoadEventProperties(textEventFields, () => [nameof(Event.Date), nameof(Event.Name), nameof(Event.Venue)]);
        set => SaveEventProperties(value, textEventFields);
    }

    internal static bool TextAlignedWithHeaders
    {
        get => Preferences.Get(textAlignedWithHeadersPreferencesKey, true);
        set => Preferences.Set(textAlignedWithHeadersPreferencesKey, value);
    }

    private static IEnumerable<PropertyInfo> LoadEventProperties(RememberedStrings remembered, Func<string[]> getDefaults)
    {
        var saved = remembered.Get();
        if (saved.Length == 0) saved = getDefaults();
        return saved.Select(name => Event.Fields.First(p => p.Name == name));
    }

    private static void SaveEventProperties(IEnumerable<PropertyInfo> value, RememberedStrings remembered)
        => remembered.Set(value.Select(p => p.Name));
}
