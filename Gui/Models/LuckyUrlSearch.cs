namespace FomoCal;

internal static class LuckyUrlSearch
{
    internal enum Engine { Google, DuckDuckGo }

    internal static string GetLabel(this Engine engine)
        => engine == Engine.Google ? "🇬 Google"
            : engine == Engine.DuckDuckGo ? "🦆 DuckDuckGo"
            : throw new ArgumentOutOfRangeException(nameof(engine));

    internal static async Task<string?> TryAsync(string query, Engine engine = Engine.Google, CancellationToken ct = default)
    {
        if (query.IsNullOrWhiteSpace()) return null;
        query += " concerts";

        string queryUrl = engine switch
        {
            Engine.Google => "https://www.google.com/search?btnI=1&q=" + Uri.EscapeDataString(query),
            Engine.DuckDuckGo => "https://duckduckgo.com/?q=" + Uri.EscapeDataString("!ducky " + query),
            _ => throw new ArgumentOutOfRangeException(nameof(engine))
        };

        // no need to follow redirects - we can pick the suggestion from the redirect target
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync(queryUrl, ct);

        // DuckDuckGo and Google both return 301/302 for top result
        if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
        {
            if (response.Headers.Location is Uri redirect)
            {
                if (engine == Engine.Google)
                {
                    var redirectQuery = System.Web.HttpUtility.ParseQueryString(redirect.Query);
                    return redirectQuery["q"];
                }

                return redirect.ToString();
            }
        }

        // other DuckDuckGo nodes return 200 with a meta http-equiv='refresh' attribute
        if (response.IsSuccessStatusCode && engine == Engine.DuckDuckGo)
        {
            var html = await response.Content.ReadAsStringAsync(ct);
            return ExtractDuckyRedirectUrl(html);
        }

        return null;
    }

    private static string? ExtractDuckyRedirectUrl(string html)
    {
        const string key = "uddg=";

        int start = html.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += key.Length;

        // find end of uddg parameter
        int end = html.IndexOf('&', start);
        if (end < 0) end = html.Length;

        string encoded = html[start..end];
        string decoded = Uri.UnescapeDataString(encoded);
        return Uri.TryCreate(decoded, UriKind.Absolute, out var uri) ? uri.ToString() : null;
    }
}