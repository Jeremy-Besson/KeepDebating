import { useEffect, useMemo, useRef, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { listDebaters, streamDebate, submitHumanAnswer } from "../api";
import { NameAvatar } from "../avatar";
import type {
  DebaterOption,
  HumanLoopCheckpoint,
  DebateRunRequest,
  DebateRunResponse,
} from "../types";
import type { LayoutOutletContext } from "../App";

const defaultRequest: DebateRunRequest = {
  topic: "Is Tamagotchi good for kids?",
  spectatorCount: 3,
  proDebaterId: "ausra",
  conDebaterId: "mindaugas",
  rounds: 4,
};

function formatSideLabel(value: string): string {
  if (value === "PRO") {
    return "For";
  }

  if (value === "CON") {
    return "Against";
  }

  return value;
}

type DebaterSide = "PRO" | "CON";

function getVerdictVisual(winner: string | null): {
  toneClass: string;
  kicker: string;
  headline: string;
  description: string;
} {
  if (winner === "PRO") {
    return {
      toneClass: "state-pro",
      kicker: "Winning Side",
      headline: "The For side carried the vote.",
      description:
        "Spectators judged the For case as the stronger overall argument.",
    };
  }

  if (winner === "CON") {
    return {
      toneClass: "state-con",
      kicker: "Winning Side",
      headline: "The Against side carried the vote.",
      description:
        "Spectators judged the Against case as the stronger overall argument.",
    };
  }

  if (winner === "TIE") {
    return {
      toneClass: "state-tie",
      kicker: "Split Decision",
      headline: "The panel ended in a draw.",
      description: "No side created enough separation to claim a clear win.",
    };
  }

  return {
    toneClass: "state-pending",
    kicker: "Live Tally",
    headline: "Votes are still coming in.",
    description:
      "The summary updates as spectator verdicts arrive and finalizes at the end of the debate.",
  };
}

function getDebaterOutcome(
  side: DebaterSide,
  winner: string | null,
): { label: string; tone: "victory" | "defeat" | "draw" } | null {
  if (!winner) {
    return null;
  }

  if (winner === "TIE") {
    return { label: "Draw", tone: "draw" };
  }

  if (winner === side) {
    return { label: "Victory", tone: "victory" };
  }

  return { label: "Defeat", tone: "defeat" };
}

function VerdictSummaryIcon({ winner }: { winner: string | null }) {
  if (winner === "PRO") {
    return (
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
      >
        <path d="M12 3l2.35 4.76 5.25.76-3.8 3.7.9 5.23L12 14.98 7.3 17.45l.9-5.23-3.8-3.7 5.25-.76L12 3z" />
      </svg>
    );
  }

  if (winner === "CON") {
    return (
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
      >
        <path d="M12 3l7 3v5c0 4.42-2.99 8.38-7 10-4.01-1.62-7-5.58-7-10V6l7-3z" />
        <path d="M9.5 12.5l1.8 1.8 3.7-4.3" />
      </svg>
    );
  }

  if (winner === "TIE") {
    return (
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
      >
        <path d="M12 4v14" />
        <path d="M8 6h8" />
        <path d="M7 8l-2.5 4h5L7 8z" />
        <path d="M17 8l-2.5 4h5L17 8z" />
        <path d="M9 20h6" />
      </svg>
    );
  }

  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
    >
      <circle cx="12" cy="12" r="8" />
      <path d="M12 8v4" />
      <circle cx="12" cy="16" r="0.9" fill="currentColor" stroke="none" />
    </svg>
  );
}

function getDebaterById(
  options: DebaterOption[],
  id: string,
): DebaterOption | null {
  return options.find((option) => option.id === id) ?? null;
}

function chooseDifferentDebater(
  options: DebaterOption[],
  currentId: string,
): string {
  const alternative = options.find((option) => option.id !== currentId);
  return alternative?.id ?? currentId;
}

function getShortCharacterSummary(character: string): string {
  const trimmed = character.trim();
  if (!trimmed) {
    return "Description pending.";
  }

  const whoIndex = trimmed.indexOf(" who ");
  const summary = whoIndex >= 0 ? trimmed.slice(0, whoIndex) : trimmed;

  if (summary.length <= 70) {
    return summary;
  }

  return `${summary.slice(0, 67).trimEnd()}...`;
}

function DebaterPicker({
  label,
  value,
  options,
  disabledId,
  onChange,
}: {
  label: string;
  value: string;
  options: DebaterOption[];
  disabledId: string;
  onChange: (id: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const selected = options.find((o) => o.id === value);
  const selectedSummary = selected
    ? getShortCharacterSummary(selected.character)
    : "Choose a debater";

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  return (
    <div className="debater-picker" ref={containerRef}>
      <span className="debater-picker-label">{label}</span>
      <button
        type="button"
        className="debater-picker-trigger"
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="listbox"
        aria-expanded={open}
      >
        <span className="debater-picker-trigger-icon">
          {selected && <NameAvatar name={selected.name} size="sm" />}
        </span>
        <span className="debater-picker-trigger-content">
          <span className="debater-picker-trigger-name">
            {selected?.name ?? "Select..."}
          </span>
          <span className="debater-picker-trigger-summary">
            {selectedSummary}
          </span>
        </span>
        <span className="debater-picker-chevron">{open ? "▲" : "▼"}</span>
      </button>
      {open ? (
        <ul className="debater-picker-list" role="listbox">
          {options.map((option) => (
            <li
              key={option.id}
              role="option"
              aria-selected={option.id === value}
              aria-disabled={option.id === disabledId}
              className={[
                "debater-picker-item",
                option.id === value ? "selected" : "",
                option.id === disabledId ? "disabled" : "",
              ]
                .filter(Boolean)
                .join(" ")}
              onClick={() => {
                if (option.id === disabledId) return;
                onChange(option.id);
                setOpen(false);
              }}
            >
              <NameAvatar name={option.name} size="sm" />
              <div>
                <strong>{option.name}</strong>
                <span>{option.character}</span>
              </div>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

export default function DebatePage() {
  const { setFocusConversation } = useOutletContext<LayoutOutletContext>();
  const [request, setRequest] = useState<DebateRunRequest>(defaultRequest);
  const [result, setResult] = useState<DebateRunResponse | null>(null);
  const [isRunning, setIsRunning] = useState(false);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [pendingCheckpoint, setPendingCheckpoint] =
    useState<HumanLoopCheckpoint | null>(null);
  const [humanAnswer, setHumanAnswer] = useState("");
  const [isSubmittingAnswer, setIsSubmittingAnswer] = useState(false);
  const [showConfig, setShowConfig] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [debaterOptions, setDebaterOptions] = useState<DebaterOption[]>([]);
  const stopStreamRef = useRef<(() => void) | null>(null);
  const turnsContainerRef = useRef<HTMLDivElement | null>(null);
  const turnCount = result?.transcript.turns.length ?? 0;
  const hasVotes = (result?.transcript.spectatorVerdicts.length ?? 0) > 0;
  const nextStance = turnCount % 2 === 0 ? "PRO" : "CON";
  const voteSummary = useMemo(() => {
    const totals = {
      proVotes: 0,
      conVotes: 0,
      tieVotes: 0,
    };

    for (const verdict of result?.transcript.spectatorVerdicts ?? []) {
      if (verdict.winner === "PRO") {
        totals.proVotes += 1;
        continue;
      }

      if (verdict.winner === "CON") {
        totals.conVotes += 1;
        continue;
      }

      totals.tieVotes += 1;
    }

    return totals;
  }, [result?.transcript.spectatorVerdicts]);
  const verdictWinner =
    result && !isRunning && hasVotes ? result.summary.finalWinner : null;
  const verdictVisual = getVerdictVisual(verdictWinner);
  const proOutcome = getDebaterOutcome("PRO", verdictWinner);
  const conOutcome = getDebaterOutcome("CON", verdictWinner);

  const selectedProDebater = useMemo(
    () => getDebaterById(debaterOptions, request.proDebaterId),
    [debaterOptions, request.proDebaterId],
  );
  const selectedConDebater = useMemo(
    () => getDebaterById(debaterOptions, request.conDebaterId),
    [debaterOptions, request.conDebaterId],
  );

  const proName = selectedProDebater?.name ?? "For debater";
  const conName = selectedConDebater?.name ?? "Against debater";
  const nextSpeakerName = nextStance === "PRO" ? proName : conName;
  const nextSpeakerCue =
    nextStance === "PRO"
      ? `${proName} is thinking...`
      : `${conName} is preparing a rebuttal...`;

  useEffect(() => {
    setFocusConversation(!showConfig && Boolean(result));
  }, [showConfig, result, setFocusConversation]);

  useEffect(() => {
    const container = turnsContainerRef.current;
    if (!container) {
      return;
    }

    const shouldAnimate = turnCount > 1;
    const frameId = window.requestAnimationFrame(() => {
      container.scrollTo({
        top: container.scrollHeight,
        behavior: shouldAnimate ? "smooth" : "auto",
      });
    });

    return () => {
      window.cancelAnimationFrame(frameId);
    };
  }, [turnCount, isRunning]);

  useEffect(() => {
    async function loadDebaters() {
      try {
        const options = await listDebaters();
        setDebaterOptions(options);

        setRequest((current) => {
          const proExists = options.some(
            (option) => option.id === current.proDebaterId,
          );
          const conExists = options.some(
            (option) => option.id === current.conDebaterId,
          );

          const safeProId = proExists
            ? current.proDebaterId
            : (options[0]?.id ?? current.proDebaterId);
          const initialConId = conExists
            ? current.conDebaterId
            : (options[1]?.id ?? options[0]?.id ?? current.conDebaterId);
          const safeConId =
            safeProId === initialConId
              ? chooseDifferentDebater(options, safeProId)
              : initialConId;

          return {
            ...current,
            proDebaterId: safeProId,
            conDebaterId: safeConId,
          };
        });
      } catch (loadError) {
        setError(
          loadError instanceof Error
            ? loadError.message
            : "Could not load debaters.",
        );
      }
    }

    loadDebaters();
  }, []);

  async function onRunDebate() {
    if (stopStreamRef.current) {
      stopStreamRef.current();
      stopStreamRef.current = null;
    }

    setIsRunning(true);
    setSessionId(null);
    setPendingCheckpoint(null);
    setHumanAnswer("");
    setShowConfig(false);
    setError(null);
    setResult({
      transcript: {
        topic: request.topic,
        model: "gpt-4.1-mini",
        startedAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
        rounds: request.rounds,
        turns: [],
        humanLoopCheckpoints: [],
        spectatorVerdicts: [],
      },
      summary: {
        finalWinner: "TIE",
        proVotes: 0,
        conVotes: 0,
        tieVotes: 0,
      },
    });

    stopStreamRef.current = streamDebate(request, {
      onStarted: (started) => {
        setSessionId(started.sessionId);
        setResult((current) =>
          current
            ? {
                ...current,
                transcript: {
                  ...current.transcript,
                  topic: started.topic,
                  rounds: started.rounds,
                },
              }
            : current,
        );
        setRequest((current) => ({
          ...current,
          proDebaterId: started.proDebater.id,
          conDebaterId: started.conDebater.id,
        }));
      },
      onTurn: (turn) => {
        setResult((current) =>
          current
            ? {
                ...current,
                transcript: {
                  ...current.transcript,
                  turns: [...current.transcript.turns, turn],
                },
              }
            : current,
        );
      },
      onHumanQuestion: (event) => {
        setSessionId(event.sessionId);
        setPendingCheckpoint(event.checkpoint);
        setHumanAnswer("");
        setResult((current) =>
          current
            ? {
                ...current,
                transcript: {
                  ...current.transcript,
                  humanLoopCheckpoints: [
                    ...current.transcript.humanLoopCheckpoints,
                    event.checkpoint,
                  ],
                },
              }
            : current,
        );
      },
      onVerdict: (verdict) => {
        setResult((current) =>
          current
            ? {
                ...current,
                transcript: {
                  ...current.transcript,
                  spectatorVerdicts: [
                    ...current.transcript.spectatorVerdicts,
                    verdict,
                  ],
                },
              }
            : current,
        );
      },
      onCompleted: (completed) => {
        setPendingCheckpoint(null);
        setResult(completed);
        setIsRunning(false);
        stopStreamRef.current = null;
      },
      onError: (message) => {
        setPendingCheckpoint(null);
        setError(message);
        setIsRunning(false);
        stopStreamRef.current = null;
      },
    });
  }

  async function onSubmitHumanAnswer() {
    if (!sessionId || !pendingCheckpoint) {
      return;
    }

    const trimmed = humanAnswer.trim();
    if (!trimmed) {
      return;
    }

    setIsSubmittingAnswer(true);
    setError(null);

    try {
      await submitHumanAnswer(sessionId, trimmed);

      setResult((current) => {
        if (!current) {
          return current;
        }

        return {
          ...current,
          transcript: {
            ...current.transcript,
            humanLoopCheckpoints: current.transcript.humanLoopCheckpoints.map(
              (checkpoint) =>
                checkpoint.checkpointId === pendingCheckpoint.checkpointId
                  ? {
                      ...checkpoint,
                      answer: trimmed,
                      status: "resolved",
                      answeredAt: new Date().toISOString(),
                    }
                  : checkpoint,
            ),
          },
        };
      });

      setPendingCheckpoint(null);
      setHumanAnswer("");
    } catch (submitError) {
      setError(
        submitError instanceof Error
          ? submitError.message
          : "Could not submit answer.",
      );
    } finally {
      setIsSubmittingAnswer(false);
    }
  }

  function onResetAndEditConfig() {
    if (stopStreamRef.current) {
      stopStreamRef.current();
      stopStreamRef.current = null;
    }

    setIsRunning(false);
    setSessionId(null);
    setPendingCheckpoint(null);
    setHumanAnswer("");
    setError(null);
    setResult(null);
    setShowConfig(true);
  }

  return (
    <>
      {!showConfig ? (
        <div className="debate-actions">
          <button
            type="button"
            className="btn-primary"
            onClick={() => {
              void onRunDebate();
            }}
            disabled={isRunning}
          >
            {isRunning ? "Streaming..." : "Restart Debate"}
          </button>
          <button
            type="button"
            className="btn-secondary"
            onClick={onResetAndEditConfig}
          >
            Reset and Edit Config
          </button>
        </div>
      ) : null}

      {result ? (
        <div className={`results-layout ${hasVotes ? "with-sidebar" : ""}`}>
          {hasVotes ? (
            <aside className="results-sidebar">
              <section className={`panel summary ${verdictVisual.toneClass}`}>
                <div className="summary-hero">
                  <div
                    className={`summary-icon ${verdictVisual.toneClass}`}
                    aria-hidden="true"
                  >
                    <VerdictSummaryIcon winner={verdictWinner} />
                  </div>
                  <div className="summary-copy">
                    <p className="summary-kicker">{verdictVisual.kicker}</p>
                    <h2>Verdict Summary</h2>
                    <p className="summary-headline">{verdictVisual.headline}</p>
                  </div>
                </div>
                <p className="summary-description">
                  {verdictVisual.description}
                </p>

                <div className="summary-stats">
                  <div className="summary-stat">
                    <span className="summary-stat-value">
                      {voteSummary.proVotes}
                    </span>
                    <span className="summary-stat-label">For</span>
                  </div>
                  <div className="summary-stat">
                    <span className="summary-stat-value">
                      {voteSummary.conVotes}
                    </span>
                    <span className="summary-stat-label">Against</span>
                  </div>
                  <div className="summary-stat">
                    <span className="summary-stat-value">
                      {voteSummary.tieVotes}
                    </span>
                    <span className="summary-stat-label">Tie</span>
                  </div>
                </div>

                <p className="summary-final-label">
                  {verdictWinner
                    ? `Final Winner: ${formatSideLabel(verdictWinner)}`
                    : "Waiting for the final panel result..."}
                </p>
              </section>

              <section className="panel votes">
                <h2>Spectator Votes</h2>
                <div className="cards">
                  {result.transcript.spectatorVerdicts.map((v) => (
                    <article key={v.spectatorName} className="card">
                      <h3 className="identity-row">
                        <NameAvatar name={v.spectatorName} size="sm" />
                        <span>{v.spectatorName}</span>
                      </h3>
                      <p className="muted">{v.perspective}</p>
                      <p className="vote-line">
                        Winner: <strong>{formatSideLabel(v.winner)}</strong> (
                        {v.confidence}%)
                      </p>
                      <details className="vote-details">
                        <summary>Details</summary>
                        <p>{v.rationale}</p>
                      </details>
                    </article>
                  ))}
                </div>
              </section>
            </aside>
          ) : null}

          <div className="conversation-column">
            <section className="panel debaters-info">
              <div className="run-meta run-meta-top">
                <div className="run-meta-item topic">
                  <span className="run-meta-label">Topic:</span>
                  <span className="run-meta-value">
                    {result.transcript.topic}
                  </span>
                </div>
              </div>

              <div className="debater-list">
                <article
                  className={[
                    "debater-chip",
                    "for",
                    proOutcome ? `outcome-${proOutcome.tone}` : "",
                  ]
                    .filter(Boolean)
                    .join(" ")}
                >
                  <h3 className="identity-row">
                    <NameAvatar name={proName} size="sm" />
                    <span>{proName}</span>
                  </h3>
                  {proOutcome ? (
                    <p className={`debater-outcome ${proOutcome.tone}`}>
                      {proOutcome.label}
                    </p>
                  ) : null}
                  <p>For</p>
                  <p className="muted">
                    {selectedProDebater?.character ?? "Character pending."}
                  </p>
                </article>
                <article
                  className={[
                    "debater-chip",
                    "against",
                    conOutcome ? `outcome-${conOutcome.tone}` : "",
                  ]
                    .filter(Boolean)
                    .join(" ")}
                >
                  <h3 className="identity-row">
                    <NameAvatar name={conName} size="sm" />
                    <span>{conName}</span>
                  </h3>
                  {conOutcome ? (
                    <p className={`debater-outcome ${conOutcome.tone}`}>
                      {conOutcome.label}
                    </p>
                  ) : null}
                  <p>Against</p>
                  <p className="muted">
                    {selectedConDebater?.character ?? "Character pending."}
                  </p>
                </article>
              </div>

              <div className="run-meta run-meta-bottom">
                <div className="run-meta-item">
                  <span className="run-meta-label">Spectators:</span>
                  <span className="run-meta-value">
                    {request.spectatorCount}
                  </span>
                </div>
                <div className="run-meta-item">
                  <span className="run-meta-label">Round:</span>
                  <span className="run-meta-value">
                    {result.transcript.rounds}
                  </span>
                </div>
              </div>
            </section>

            <section className="panel timeline">
              <h2>Conversation</h2>

              {pendingCheckpoint ? (
                <article className="turn thinking">
                  <header className="identity-row">
                    <span>Orchestrator question</span>
                  </header>
                  <p>{pendingCheckpoint.question}</p>
                  <p className="muted">Reason: {pendingCheckpoint.reason}</p>
                  <div className="form-grid">
                    <label className="form-grid-span-2">
                      Your answer
                      <input
                        value={humanAnswer}
                        onChange={(e) => setHumanAnswer(e.target.value)}
                        placeholder="Type the human-in-the-loop guidance..."
                      />
                    </label>
                  </div>
                  <button
                    type="button"
                    className="btn-primary"
                    onClick={() => {
                      void onSubmitHumanAnswer();
                    }}
                    disabled={
                      isSubmittingAnswer || humanAnswer.trim().length === 0
                    }
                  >
                    {isSubmittingAnswer ? "Sending..." : "Send answer"}
                  </button>
                </article>
              ) : null}

              <div ref={turnsContainerRef} className="turns">
                {result.transcript.turns.map((turn, index) => (
                  <article
                    key={`${turn.round}-${index}`}
                    className={`turn ${(turn.stance ?? "unknown").toLowerCase()}`}
                  >
                    <header className="identity-row">
                      <NameAvatar name={turn.speaker} size="sm" />
                      <span>{turn.speaker}</span>
                    </header>
                    <p>{turn.message}</p>
                    {turn.turnKind === "follow-up" ? (
                      <p className="muted">
                        Follow-up selected by orchestrator.
                      </p>
                    ) : null}
                    {turn.orchestratorReason ? (
                      <p className="muted">Reason: {turn.orchestratorReason}</p>
                    ) : null}
                  </article>
                ))}

                {result.transcript.humanLoopCheckpoints
                  .filter((checkpoint) => checkpoint.status !== "pending")
                  .map((checkpoint) => (
                    <article
                      key={checkpoint.checkpointId}
                      className="turn thinking"
                    >
                      <header className="identity-row">
                        <span>Human checkpoint</span>
                      </header>
                      <p>{checkpoint.question}</p>
                      <p className="muted">Reason: {checkpoint.reason}</p>
                      {checkpoint.answer ? (
                        <p>
                          <strong>Answer:</strong> {checkpoint.answer}
                        </p>
                      ) : (
                        <p className="muted">Awaiting answer...</p>
                      )}
                    </article>
                  ))}

                {isRunning ? (
                  <article className="turn thinking">
                    <header className="identity-row">
                      <NameAvatar name={nextSpeakerName} size="sm" />
                      <span>{nextSpeakerName}</span>
                    </header>
                    <p>{nextSpeakerCue}</p>
                  </article>
                ) : null}
              </div>
            </section>
          </div>
        </div>
      ) : null}

      {!showConfig && error ? <p className="error panel">{error}</p> : null}

      {showConfig ? (
        <section className="panel config">
          <h2>Debate Config</h2>
          <div className="form-grid">
            <label className="form-grid-span-2">
              Topic
              <input
                value={request.topic}
                onChange={(e) =>
                  setRequest((current) => ({
                    ...current,
                    topic: e.target.value,
                  }))
                }
              />
            </label>
            <label>
              Spectators
              <input
                type="number"
                min={1}
                max={14}
                value={request.spectatorCount}
                onChange={(e) =>
                  setRequest((current) => ({
                    ...current,
                    spectatorCount: Number(e.target.value),
                  }))
                }
              />
            </label>
            <label>
              Rounds
              <input
                type="number"
                min={1}
                max={20}
                value={request.rounds}
                onChange={(e) =>
                  setRequest((current) => ({
                    ...current,
                    rounds: Number(e.target.value),
                  }))
                }
              />
            </label>
          </div>
          <div className="debater-pickers">
            <DebaterPicker
              label="For"
              value={request.proDebaterId}
              options={debaterOptions}
              disabledId={request.proDebaterId}
              onChange={(nextProId) =>
                setRequest((current) => ({
                  ...current,
                  proDebaterId: nextProId,
                }))
              }
            />
            <DebaterPicker
              label="Against"
              value={request.conDebaterId}
              options={debaterOptions}
              disabledId={request.conDebaterId}
              onChange={(nextConId) =>
                setRequest((current) => ({
                  ...current,
                  conDebaterId: nextConId,
                }))
              }
            />
          </div>
          <button
            type="button"
            className="btn-primary"
            onClick={onRunDebate}
            disabled={isRunning}
          >
            {isRunning ? "Streaming..." : "Run Debate"}
          </button>
          {error ? <p className="error">{error}</p> : null}
        </section>
      ) : null}
    </>
  );
}
