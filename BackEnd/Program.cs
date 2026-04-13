using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using System.Text.Json;
using TryingStuff.Models;
using TryingStuff.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<DebateSessionStore>();

var app = builder.Build();
var sseJsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

app.UseCors("frontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "Debaters BackEnd" }));

app.MapGet("/api/debaters", () => Results.Ok(DebaterCatalog.GetAll()));

app.MapPost("/api/debates/run", async (
    DebateRunRequest request,
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    DebateSessionStore sessionStore) =>
{
    var wikipediaCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var knowledgeStore = CreateKnowledgeStore(configuration, loggerFactory);
    var runtime = CreateRuntime(configuration, loggerFactory, wikipediaCache, knowledgeStore);
    if (!runtime.Success)
    {
        return Results.BadRequest(new { error = runtime.ErrorMessage });
    }

    var session = CreateSessionState(request, runtime.Model!, wikipediaCache, knowledgeStore);
    sessionStore.Create(session);

    var progress = await runtime.Orchestrator!.ContinueDebateAsync(session, waitForHumanInput: false);
    if (progress.Status == "needs-input")
    {
        return Results.Ok(new DebateSessionResponse
        {
            Status = "needs-input",
            SessionId = session.SessionId,
            PendingCheckpoint = progress.PendingCheckpoint
        });
    }

    if (progress.Status == "completed")
    {
        await CompleteSessionAsync(session, runtime.Panel!);
        sessionStore.TryRemove(session.SessionId);

        return Results.Ok(new DebateSessionResponse
        {
            Status = "completed",
            SessionId = session.SessionId,
            Result = new DebateRunResponse
            {
                Transcript = session.Transcript,
                Summary = BuildSummary(session.Transcript)
            }
        });
    }

    return Results.Ok(new DebateSessionResponse
    {
        Status = "running",
        SessionId = session.SessionId
    });
});

app.MapPost("/api/debates/answer", async (
    DebateAnswerRequest request,
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    DebateSessionStore sessionStore) =>
{
    if (!sessionStore.TryGet(request.SessionId, out var session) || session is null)
    {
        return Results.NotFound(new { error = "Session not found." });
    }

    if (!sessionStore.SubmitAnswer(request.SessionId, request.Answer))
    {
        return Results.BadRequest(new { error = "No pending human question for this session." });
    }

    if (!request.AutoContinue)
    {
        return Results.Ok(new DebateSessionResponse
        {
            Status = "answer-accepted",
            SessionId = request.SessionId
        });
    }

    var runtime = CreateRuntime(configuration, loggerFactory, session.WikipediaCache, session.KnowledgeStore);
    if (!runtime.Success)
    {
        return Results.BadRequest(new { error = runtime.ErrorMessage });
    }

    var progress = await runtime.Orchestrator!.ContinueDebateAsync(session, waitForHumanInput: false);
    if (progress.Status == "needs-input")
    {
        return Results.Ok(new DebateSessionResponse
        {
            Status = "needs-input",
            SessionId = session.SessionId,
            PendingCheckpoint = progress.PendingCheckpoint
        });
    }

    if (progress.Status == "completed")
    {
        await CompleteSessionAsync(session, runtime.Panel!);
        sessionStore.TryRemove(session.SessionId);

        return Results.Ok(new DebateSessionResponse
        {
            Status = "completed",
            SessionId = session.SessionId,
            Result = new DebateRunResponse
            {
                Transcript = session.Transcript,
                Summary = BuildSummary(session.Transcript)
            }
        });
    }

    return Results.Ok(new DebateSessionResponse
    {
        Status = "running",
        SessionId = session.SessionId
    });
});

app.MapGet("/api/debates/stream", async (
    HttpResponse response,
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    DebateSessionStore sessionStore,
    string? topic,
    int? rounds,
    int? spectatorCount,
    string? proDebaterId,
    string? conDebaterId) =>
{
    var request = new DebateRunRequest
    {
        Topic = topic,
        Rounds = rounds,
        SpectatorCount = spectatorCount,
        ProDebaterId = proDebaterId,
        ConDebaterId = conDebaterId
    };

    var wikipediaCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var knowledgeStore = CreateKnowledgeStore(configuration, loggerFactory);
    var runtime = CreateRuntime(configuration, loggerFactory, wikipediaCache, knowledgeStore);

    response.Headers.ContentType = "text/event-stream";
    response.Headers.CacheControl = "no-cache";
    response.Headers.Connection = "keep-alive";

    if (!runtime.Success)
    {
        await WriteSseEvent(response, "stream-error", new
        {
            message = runtime.ErrorMessage
        });
        return;
    }

    var session = CreateSessionState(request, runtime.Model!, wikipediaCache, knowledgeStore);
    sessionStore.Create(session);

    try
    {
        await WriteSseEvent(response, "started", new
        {
            sessionId = session.SessionId,
            topic = session.Transcript.Topic,
            rounds = session.Transcript.Rounds,
            spectatorCount = session.SpectatorCount,
            proDebater = session.ProPersona,
            conDebater = session.ConPersona
        });

        while (true)
        {
            var progress = await runtime.Orchestrator!.ContinueDebateAsync(
                session,
                waitForHumanInput: true,
                turn => WriteSseEvent(response, "turn", turn),
                checkpoint => WriteSseEvent(response, "human-question", new
                {
                    sessionId = session.SessionId,
                    checkpoint
                }),
                response.HttpContext.RequestAborted);

            if (progress.Status == "completed")
            {
                break;
            }

            if (progress.Status == "waiting-answer" && progress.PendingCheckpoint is not null)
            {
                await sessionStore.WaitForAnswerAsync(
                    session.SessionId,
                    progress.PendingCheckpoint.CheckpointId,
                    response.HttpContext.RequestAborted);
                continue;
            }

            throw new InvalidOperationException($"Unexpected stream progress status: {progress.Status}");
        }

        await CompleteSessionAsync(
            session,
            runtime.Panel!,
            verdict => WriteSseEvent(response, "verdict", verdict));

        var completed = new DebateRunResponse
        {
            Transcript = session.Transcript,
            Summary = BuildSummary(session.Transcript)
        };

        await WriteSseEvent(response, "completed", completed);
        sessionStore.TryRemove(session.SessionId);
    }
    catch (Exception ex)
    {
        await WriteSseEvent(response, "stream-error", new
        {
            message = $"Stream failed: {ex.Message}"
        });
    }
});

app.Run();

async Task WriteSseEvent(HttpResponse response, string eventName, object payload)
{
    var json = JsonSerializer.Serialize(payload, sseJsonOptions);
    await response.WriteAsync($"event: {eventName}\n");
    await response.WriteAsync($"data: {json}\n\n");
    await response.Body.FlushAsync();
}

static DebateSessionState CreateSessionState(
    DebateRunRequest request,
    string model,
    Dictionary<string, string> wikipediaCache,
    DebateKnowledgeStore knowledgeStore)
{
    var selectedDebaters = DebaterCatalog.ResolvePair(request.ProDebaterId, request.ConDebaterId);
    var safeTopic = request.Topic ?? "Is Tamagotchi good for kids?";
    var safeRounds = Math.Clamp(request.Rounds ?? 3, 1, 20);
    var safeSpectatorCount = Math.Clamp(request.SpectatorCount ?? 3, 1, 14);
    var startedAt = DateTimeOffset.UtcNow;

    return new DebateSessionState
    {
        SessionId = Guid.NewGuid().ToString("N"),
        SpectatorCount = safeSpectatorCount,
        ProPersona = selectedDebaters.Pro,
        ConPersona = selectedDebaters.Con,
        WikipediaCache = wikipediaCache,
        KnowledgeStore = knowledgeStore,
        Transcript = new DebateTranscript
        {
            Topic = safeTopic,
            Model = model,
            StartedAt = startedAt,
            CompletedAt = startedAt,
            Rounds = safeRounds,
            Turns = [],
            HumanLoopCheckpoints = [],
            SpectatorVerdicts = []
        }
    };
}

static async Task CompleteSessionAsync(
    DebateSessionState session,
    DebateSpectatorPanel panel,
    Func<SpectatorVerdict, Task>? onVerdictGenerated = null)
{
    if (session.Transcript.SpectatorVerdicts.Count > 0)
    {
        return;
    }

    session.Transcript.SpectatorVerdicts.AddRange(await panel.EvaluateAsync(
        session.Transcript,
        session.SpectatorCount,
        onVerdictGenerated));
}

static DebateKnowledgeStore CreateKnowledgeStore(IConfiguration configuration, ILoggerFactory loggerFactory)
{
    var endpoint = GetBaseEndpoint(configuration["AzureOpenAI:EmbeddingEndpoint"]);
    var apiKey = configuration["AzureOpenAI:EmbeddingApiKey"] ?? string.Empty;
    var embeddingDeployment = configuration["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-3-small";

#pragma warning disable SKEXP0010 // AddAzureOpenAIEmbeddingGenerator is experimental in SemanticKernel
    var embeddingGenerator = Kernel.CreateBuilder()
        .AddAzureOpenAIEmbeddingGenerator(embeddingDeployment, endpoint, apiKey)
        .Build()
        .GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
#pragma warning restore SKEXP0010

    return new DebateKnowledgeStore(embeddingGenerator, loggerFactory.CreateLogger<DebateKnowledgeStore>());
}

static RuntimeFactoryResult CreateRuntime(
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    Dictionary<string, string> wikipediaCache,
    DebateKnowledgeStore knowledgeStore)
{
    var endpoint = NormalizeAzureEndpoint(configuration["AzureOpenAI:Endpoint"]);
    var apiKey = configuration["AzureOpenAI:ApiKey"];
    var model = configuration["AzureOpenAI:Model"] ?? "gpt-4.1-mini";
    var debateStyle = configuration["DebateStyle:OverallStyle"];
    var proTone = configuration["DebateStyle:ProTone"];
    var conTone = configuration["DebateStyle:ConTone"];
    var minTurnDelaySeconds = configuration.GetValue<int>("DebateStyle:MinTurnDelaySeconds", 0);

    if (!HasValidAzureConfiguration(endpoint, apiKey))
    {
        return RuntimeFactoryResult.FromError("Invalid AzureOpenAI configuration. Set real AzureOpenAI:Endpoint and AzureOpenAI:ApiKey in BackEnd/appsettings.Development.json or environment variables.");
    }

    var client = new ChatCompletionsClient(
        new Uri(endpoint!),
        new AzureKeyCredential(apiKey!),
        new AzureAIInferenceClientOptions());

    var brain = new DebateBrainOrchestrator(
        endpoint!,
        apiKey!,
        model,
        wikipediaCache,
        knowledgeStore,
        loggerFactory.CreateLogger<DebateBrainOrchestrator>(),
        loggerFactory.CreateLogger<WikipediaPlugin>());

    var orchestrator = new DebateOrchestrator(
        client,
        brain,
        knowledgeStore,
        model,
        loggerFactory.CreateLogger<DebateOrchestrator>(),
        debateStyle,
        proTone,
        conTone,
        minTurnDelaySeconds);

    var panel = new DebateSpectatorPanel(client, model);

    return RuntimeFactoryResult.FromSuccess(model, orchestrator, panel);
}

static DebateSummary BuildSummary(DebateTranscript transcript)
{
    var proVotes = transcript.SpectatorVerdicts.Count(v => v.Winner == "PRO");
    var conVotes = transcript.SpectatorVerdicts.Count(v => v.Winner == "CON");
    var tieVotes = transcript.SpectatorVerdicts.Count(v => v.Winner == "TIE");

    var finalWinner = tieVotes >= proVotes && tieVotes >= conVotes
        ? "TIE"
        : (proVotes == conVotes ? "TIE" : (proVotes > conVotes ? "PRO" : "CON"));

    return new DebateSummary
    {
        FinalWinner = finalWinner,
        ProVotes = proVotes,
        ConVotes = conVotes,
        TieVotes = tieVotes
    };
}

static bool HasValidAzureConfiguration(string? endpoint, string? apiKey)
{
    if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
    {
        return false;
    }

    var endpointLooksPlaceholder = endpoint.Contains("your-resource", StringComparison.OrdinalIgnoreCase);
    var keyLooksPlaceholder = apiKey.Contains("replace-with-your-api-key", StringComparison.OrdinalIgnoreCase);

    return !endpointLooksPlaceholder && !keyLooksPlaceholder;
}

static string GetBaseEndpoint(string? endpoint)
{
    if (string.IsNullOrWhiteSpace(endpoint))
    {
        return string.Empty;
    }

    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
    {
        return endpoint.TrimEnd('/');
    }

    return $"{uri.Scheme}://{uri.Authority}";
}

static string? NormalizeAzureEndpoint(string? endpoint)
{
    if (string.IsNullOrWhiteSpace(endpoint))
    {
        return endpoint;
    }

    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
    {
        return endpoint;
    }

    var path = uri.AbsolutePath;
    var marker = "/chat/completions";
    var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    if (markerIndex >= 0)
    {
        path = path[..markerIndex];
    }

    var builder = new UriBuilder(uri)
    {
        Path = path,
        Query = string.Empty
    };

    return builder.Uri.AbsoluteUri.TrimEnd('/');
}

namespace TryingStuff.Models
{

    public sealed class DebateRunRequest
    {
        public string? Topic { get; init; }
        public int? Rounds { get; init; }
        public int? SpectatorCount { get; init; }
        public string? ProDebaterId { get; init; }
        public string? ConDebaterId { get; init; }
    }

    public sealed class DebateAnswerRequest
    {
        public required string SessionId { get; init; }
        public required string Answer { get; init; }
        public bool AutoContinue { get; init; }
    }

    public sealed class DebateRunResponse
    {
        public required DebateTranscript Transcript { get; init; }
        public required DebateSummary Summary { get; init; }
    }

    public sealed class DebateSessionResponse
    {
        public required string Status { get; init; }
        public required string SessionId { get; init; }
        public HumanLoopCheckpoint? PendingCheckpoint { get; init; }
        public DebateRunResponse? Result { get; init; }
    }

    public sealed class DebateSummary
    {
        public required string FinalWinner { get; init; }
        public required int ProVotes { get; init; }
        public required int ConVotes { get; init; }
        public required int TieVotes { get; init; }
    }

    public sealed class RuntimeFactoryResult
    {
        public required bool Success { get; init; }
        public string? Model { get; init; }
        public string? ErrorMessage { get; init; }
        public DebateOrchestrator? Orchestrator { get; init; }
        public DebateSpectatorPanel? Panel { get; init; }

        public static RuntimeFactoryResult FromError(string message)
        {
            return new RuntimeFactoryResult
            {
                Success = false,
                ErrorMessage = message
            };
        }

        public static RuntimeFactoryResult FromSuccess(string model, DebateOrchestrator orchestrator, DebateSpectatorPanel panel)
        {
            return new RuntimeFactoryResult
            {
                Success = true,
                Model = model,
                Orchestrator = orchestrator,
                Panel = panel
            };
        }
    }
}
