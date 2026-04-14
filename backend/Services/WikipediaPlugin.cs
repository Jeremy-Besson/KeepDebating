using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace TryingStuff.Services;

public sealed class WikipediaPlugin
{
    private const string SearchBaseUrl = "https://en.wikipedia.org/w/api.php";
    private const string SummaryBaseUrl = "https://en.wikipedia.org/api/rest_v1/page/summary";
    private const int ResponsePreviewLength = 300;

    private static readonly Action<ILogger, string, Exception?> LogWikipediaSearchStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1100, nameof(LogWikipediaSearchStarted)),
            "Wikipedia tool search started. Query: {Query}");

    private static readonly Action<ILogger, string, string, Exception?> LogWikipediaSearchCompleted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(1101, nameof(LogWikipediaSearchCompleted)),
            "Wikipedia tool search completed. Query: {Query}. Response: {Response}");

    private static readonly Action<ILogger, string, string, Exception?> LogWikipediaSearchCacheHit =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(1102, nameof(LogWikipediaSearchCacheHit)),
            "Wikipedia tool search cache hit. Query: {Query}. Response: {Response}");

    private static readonly Action<ILogger, string, string, Exception?> LogWikipediaSearchFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(1103, nameof(LogWikipediaSearchFailed)),
            "Wikipedia tool search failed. Query: {Query}. ExceptionType: {ExceptionType}");

    private static readonly Action<ILogger, string, string, Exception?> LogKnowledgeStoreStoreFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1105, nameof(LogKnowledgeStoreStoreFailed)),
            "Knowledge store embedding failed (Wikipedia result still returned). Query: {Query}. ExceptionType: {ExceptionType}");

    private static readonly Action<ILogger, string, double, Exception?> LogWikipediaSearchTelemetry =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(1104, nameof(LogWikipediaSearchTelemetry)),
            "Wikipedia tool search telemetry. Query: {Query}. DurationMs: {DurationMs}");

    private static readonly HttpClient Http = CreateHttpClient();
    private readonly Dictionary<string, string> _cache;
    private readonly DebateKnowledgeStore _knowledgeStore;
    private readonly ILogger<WikipediaPlugin> _logger;

    public WikipediaPlugin(Dictionary<string, string> cache, DebateKnowledgeStore knowledgeStore, ILogger<WikipediaPlugin> logger)
    {
        _cache = cache;
        _knowledgeStore = knowledgeStore;
        _logger = logger;
    }

    [KernelFunction("search")]
    [Description("Searches Wikipedia for a topic and returns a concise, source-attributed summary.")]
    public async Task<string> SearchAsync(
        [Description("The query to search on Wikipedia. Keep it specific to the debate claim.")] string query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = (query ?? string.Empty).Trim();
        var startedAt = Stopwatch.GetTimestamp();

        LogWikipediaSearchStarted(_logger, normalizedQuery, null);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var emptyResponse = "No Wikipedia results: empty query.";
            LogWikipediaSearchCompleted(_logger, normalizedQuery, FormatResponseForLogs(emptyResponse), null);
            return emptyResponse;
        }

        var cacheKey = normalizedQuery.ToLowerInvariant();
        lock (_cache)
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                LogWikipediaSearchCacheHit(_logger, normalizedQuery, FormatResponseForLogs(cached), null);
                return cached;
            }
        }

        try
        {
            var encodedQuery = Uri.EscapeDataString(normalizedQuery);
            var searchUrl =
                $"{SearchBaseUrl}?action=query&list=search&srsearch={encodedQuery}&format=json&utf8=1&srlimit=3";

            using var searchResponse = await Http.GetAsync(searchUrl, cancellationToken);
            if (!searchResponse.IsSuccessStatusCode)
            {
                var failedSearchResponse = $"No Wikipedia results: search request failed ({(int)searchResponse.StatusCode}).";
                LogWikipediaSearchCompleted(_logger, normalizedQuery, FormatResponseForLogs(failedSearchResponse), null);
                return failedSearchResponse;
            }

            await using var searchStream = await searchResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var searchJson = await JsonDocument.ParseAsync(searchStream, cancellationToken: cancellationToken);

            if (!TryGetTopTitle(searchJson.RootElement, out var topTitle) || string.IsNullOrWhiteSpace(topTitle))
            {
                const string noMatchResponse = "No Wikipedia results: no matching article found.";
                LogWikipediaSearchCompleted(_logger, normalizedQuery, FormatResponseForLogs(noMatchResponse), null);
                return noMatchResponse;
            }

            var encodedTitle = Uri.EscapeDataString(topTitle);
            var summaryUrl = $"{SummaryBaseUrl}/{encodedTitle}";

            using var summaryResponse = await Http.GetAsync(summaryUrl, cancellationToken);
            if (!summaryResponse.IsSuccessStatusCode)
            {
                var noSummaryResponse = $"No Wikipedia summary available for '{topTitle}'.";
                LogWikipediaSearchCompleted(_logger, normalizedQuery, FormatResponseForLogs(noSummaryResponse), null);
                return noSummaryResponse;
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

            try
            {
                await _knowledgeStore.StoreAsync(cacheKey, result, cancellationToken);
            }
            catch (Exception storeEx)
            {
                LogKnowledgeStoreStoreFailed(_logger, normalizedQuery, storeEx.GetType().Name, storeEx);
            }

            LogWikipediaSearchCompleted(_logger, normalizedQuery, FormatResponseForLogs(result), null);
            return result;
        }
        catch (Exception ex)
        {
            LogWikipediaSearchFailed(_logger, normalizedQuery, ex.GetType().Name, ex);
            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            LogWikipediaSearchTelemetry(_logger, normalizedQuery, elapsed.TotalMilliseconds, null);
        }
    }

    private static string FormatResponseForLogs(string response)
    {
        var preview = response.Length > ResponsePreviewLength
            ? string.Concat(response.AsSpan(0, ResponsePreviewLength), "...")
            : response;

        return $"Length={response.Length}; Preview={preview}; Full={response}";
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