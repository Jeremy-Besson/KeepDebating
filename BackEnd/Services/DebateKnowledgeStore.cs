using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace TryingStuff.Services;

public sealed class DebateKnowledgeStore : IDisposable
{
    private static readonly Action<ILogger, string, int, Exception?> LogStored =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(1200, nameof(LogStored)),
            "Knowledge store: stored entry. Id: {Id}. Total entries: {Count}");

    private static readonly Action<ILogger, string, int, string, Exception?> LogRetrieved =
        LoggerMessage.Define<string, int, string>(
            LogLevel.Information,
            new EventId(1201, nameof(LogRetrieved)),
            "Knowledge store: retrieved top-{TopK} results for query: {Query}. Results: {Results}");

    private static readonly Action<ILogger, string, Exception?> LogSkippedDuplicate =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1202, nameof(LogSkippedDuplicate)),
            "Knowledge store: skipped duplicate entry. Id: {Id}");

    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly List<KnowledgeEntry> _entries = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<DebateKnowledgeStore> _logger;

    public DebateKnowledgeStore(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, ILogger<DebateKnowledgeStore> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    public async Task StoreAsync(string id, string text, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_entries.Any(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                LogSkippedDuplicate(_logger, id, null);
                return;
            }
        }
        finally
        {
            _lock.Release();
        }

        var embeddings = await _embeddingGenerator.GenerateAsync([text], cancellationToken: cancellationToken);
        var embedding = embeddings[0].Vector;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_entries.Any(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                _entries.Add(new KnowledgeEntry(id, text, embedding));
            }

            LogStored(_logger, id, _entries.Count, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> RetrieveAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        List<KnowledgeEntry> snapshot;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_entries.Count == 0)
            {
                return [];
            }

            snapshot = [.. _entries];
        }
        finally
        {
            _lock.Release();
        }

        var queryEmbeddings = await _embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);
        var queryVector = queryEmbeddings[0].Vector;

        var results = snapshot
            .Select(e => (e.Text, Score: CosineSimilarity(queryVector.Span, e.Embedding.Span)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Text)
            .ToList();

        var resultsSummary = results.Count == 0
            ? "(none)"
            : string.Join(" | ", results.Select((r, i) => $"[{i + 1}] {(r.Length > 120 ? r[..120] + "..." : r)}"));

        LogRetrieved(_logger, query, topK, resultsSummary, null);
        return results;
    }

    public void Dispose() => _lock.Dispose();

    private static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float dot = 0f, magA = 0f, magB = 0f;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denominator < 1e-10f ? 0f : dot / denominator;
    }

    private sealed record KnowledgeEntry(string Id, string Text, ReadOnlyMemory<float> Embedding);
}
