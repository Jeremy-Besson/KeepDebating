using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace TryingStuff.Services;

public sealed class WikipediaPlugin
{
    private const string SearchBaseUrl = "https://en.wikipedia.org/w/api.php";
    private const string SummaryBaseUrl = "https://en.wikipedia.org/api/rest_v1/page/summary";

    private static readonly HttpClient Http = CreateHttpClient();
    private readonly Dictionary<string, string> _cache;

    public WikipediaPlugin(Dictionary<string, string> cache)
    {
        _cache = cache;
    }

    [KernelFunction("search")]
    [Description("Searches Wikipedia for a topic and returns a concise, source-attributed summary.")]
    public async Task<string> SearchAsync(
        [Description("The query to search on Wikipedia. Keep it specific to the debate claim.")] string query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return "No Wikipedia results: empty query.";
        }

        var cacheKey = normalizedQuery.ToLowerInvariant();
        lock (_cache)
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }

        var encodedQuery = Uri.EscapeDataString(normalizedQuery);
        var searchUrl =
            $"{SearchBaseUrl}?action=query&list=search&srsearch={encodedQuery}&format=json&utf8=1&srlimit=3";

        using var searchResponse = await Http.GetAsync(searchUrl, cancellationToken);
        if (!searchResponse.IsSuccessStatusCode)
        {
            return $"No Wikipedia results: search request failed ({(int)searchResponse.StatusCode}).";
        }

        await using var searchStream = await searchResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var searchJson = await JsonDocument.ParseAsync(searchStream, cancellationToken: cancellationToken);

        if (!TryGetTopTitle(searchJson.RootElement, out var topTitle) || string.IsNullOrWhiteSpace(topTitle))
        {
            return "No Wikipedia results: no matching article found.";
        }

        var encodedTitle = Uri.EscapeDataString(topTitle);
        var summaryUrl = $"{SummaryBaseUrl}/{encodedTitle}";

        using var summaryResponse = await Http.GetAsync(summaryUrl, cancellationToken);
        if (!summaryResponse.IsSuccessStatusCode)
        {
            return $"No Wikipedia summary available for '{topTitle}'.";
        }

        await using var summaryStream = await summaryResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var summaryJson = await JsonDocument.ParseAsync(summaryStream, cancellationToken: cancellationToken);

        var title = summaryJson.RootElement.TryGetProperty("title", out var titleNode)
            ? titleNode.GetString() ?? topTitle
            : topTitle;

        var extract = summaryJson.RootElement.TryGetProperty("extract", out var extractNode)
            ? extractNode.GetString() ?? ""
            : "";

        var canonicalUrl = TryGetCanonicalUrl(summaryJson.RootElement)
            ?? $"https://en.wikipedia.org/wiki/{encodedTitle}";

        var condensedExtract = extract.Length > 500 ? string.Concat(extract.AsSpan(0, 500), "...") : extract;
        var result = $"Wikipedia: {title}. {condensedExtract} Source: {canonicalUrl}";

        lock (_cache)
        {
            _cache[cacheKey] = result;
        }

        return result;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TryingStuffDebateBot/1.0 (https://localhost)");
        return client;
    }

    private static bool TryGetTopTitle(JsonElement root, out string? title)
    {
        title = null;

        if (!root.TryGetProperty("query", out var queryNode)
            || !queryNode.TryGetProperty("search", out var searchNode)
            || searchNode.ValueKind != JsonValueKind.Array
            || searchNode.GetArrayLength() == 0)
        {
            return false;
        }

        var first = searchNode[0];
        if (!first.TryGetProperty("title", out var titleNode))
        {
            return false;
        }

        title = titleNode.GetString();
        return !string.IsNullOrWhiteSpace(title);
    }

    private static string? TryGetCanonicalUrl(JsonElement root)
    {
        if (!root.TryGetProperty("content_urls", out var contentUrls)
            || !contentUrls.TryGetProperty("desktop", out var desktop)
            || !desktop.TryGetProperty("page", out var page))
        {
            return null;
        }

        return page.GetString();
    }
}