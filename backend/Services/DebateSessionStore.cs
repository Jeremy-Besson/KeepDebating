using System.Collections.Concurrent;
using TryingStuff.Models;

namespace TryingStuff.Services;

public sealed class DebateSessionStore
{
    private readonly ConcurrentDictionary<string, DebateSessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public DebateSessionState Create(DebateSessionState state)
    {
        _sessions[state.SessionId] = state;
        return state;
    }

    public bool TryGet(string sessionId, out DebateSessionState? state)
    {
        if (_sessions.TryGetValue(sessionId, out var found))
        {
            state = found;
            return true;
        }

        state = null;
        return false;
    }

    public bool TryRemove(string sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
    }

    public bool SubmitAnswer(string sessionId, string answer)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            return false;
        }

        lock (state.SyncRoot)
        {
            if (state.PendingCheckpointId is null)
            {
                return false;
            }

            var checkpoint = state.Transcript.HumanLoopCheckpoints
                .FirstOrDefault(c => c.CheckpointId == state.PendingCheckpointId);

            if (checkpoint is null)
            {
                return false;
            }

            checkpoint.Answer = answer;
            checkpoint.Status = "resolved";
            checkpoint.AnsweredAt = DateTimeOffset.UtcNow;

            state.LastHumanAnswer = answer;
            state.PendingCheckpointId = null;

            state.PendingAnswerSignal?.TrySetResult(answer);
            state.PendingAnswerSignal = null;
        }

        return true;
    }

    public Task<string> WaitForAnswerAsync(string sessionId, string checkpointId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            throw new InvalidOperationException("Session was not found.");
        }

        TaskCompletionSource<string> signal;

        lock (state.SyncRoot)
        {
            if (!string.Equals(state.PendingCheckpointId, checkpointId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Checkpoint is not pending.");
            }

            signal = state.PendingAnswerSignal ??= new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        cancellationToken.Register(() => signal.TrySetCanceled(cancellationToken));
        return signal.Task;
    }
}

public sealed class DebateSessionState
{
    public required string SessionId { get; init; }
    public required DebateTranscript Transcript { get; init; }
    public required int SpectatorCount { get; init; }
    public required DebaterPersona ProPersona { get; init; }
    public required DebaterPersona ConPersona { get; init; }
    public required Dictionary<string, string> WikipediaCache { get; init; }

    public string? PendingCheckpointId { get; set; }
    public string? LastHumanAnswer { get; set; }
    public bool IsCompleted { get; set; }

    public Dictionary<int, bool> FollowUpUsedByRound { get; } = [];
    public object SyncRoot { get; } = new();
    public TaskCompletionSource<string>? PendingAnswerSignal { get; set; }
}
