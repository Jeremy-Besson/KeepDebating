import type {
  DebateHumanQuestionEvent,
  DebateRunRequest,
  DebateRunResponse,
  DebateStreamStarted,
  DebateTurn,
  DebaterOption,
  SpectatorVerdict,
} from "./types";

export async function runDebate(request: DebateRunRequest): Promise<DebateRunResponse> {
  const response = await fetch("/api/debates/run", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(request)
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed with status ${response.status}`);
  }

  return (await response.json()) as DebateRunResponse;
}

export async function listDebaters(): Promise<DebaterOption[]> {
  const response = await fetch("/api/debaters");

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed with status ${response.status}`);
  }

  return (await response.json()) as DebaterOption[];
}

type StreamHandlers = {
  onStarted: (started: DebateStreamStarted) => void;
  onTurn: (turn: DebateTurn) => void;
  onVerdict: (verdict: SpectatorVerdict) => void;
  onHumanQuestion: (event: DebateHumanQuestionEvent) => void;
  onCompleted: (completed: DebateRunResponse) => void;
  onError: (message: string) => void;
};

export async function submitHumanAnswer(
  sessionId: string,
  answer: string,
): Promise<void> {
  const response = await fetch("/api/debates/answer", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      sessionId,
      answer,
      autoContinue: false,
    }),
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed with status ${response.status}`);
  }
}

export function streamDebate(request: DebateRunRequest, handlers: StreamHandlers): () => void {
  const params = new URLSearchParams({
    topic: request.topic,
    rounds: String(request.rounds),
    spectatorCount: String(request.spectatorCount),
    proDebaterId: request.proDebaterId,
    conDebaterId: request.conDebaterId,
  });

  const source = new EventSource(`/api/debates/stream?${params.toString()}`);

  source.addEventListener("started", (event) => {
    handlers.onStarted(JSON.parse((event as MessageEvent).data) as DebateStreamStarted);
  });

  source.addEventListener("turn", (event) => {
    handlers.onTurn(JSON.parse((event as MessageEvent).data) as DebateTurn);
  });

  source.addEventListener("verdict", (event) => {
    handlers.onVerdict(JSON.parse((event as MessageEvent).data) as SpectatorVerdict);
  });

  source.addEventListener("human-question", (event) => {
    handlers.onHumanQuestion(
      JSON.parse((event as MessageEvent).data) as DebateHumanQuestionEvent,
    );
  });

  source.addEventListener("completed", (event) => {
    handlers.onCompleted(JSON.parse((event as MessageEvent).data) as DebateRunResponse);
    source.close();
  });

  source.addEventListener("stream-error", (event) => {
    try {
      const payload = JSON.parse((event as MessageEvent).data) as { message?: string };
      handlers.onError(payload.message ?? "Live stream failed. Check backend logs and retry.");
    } catch {
      handlers.onError("Live stream failed. Check backend logs and retry.");
    }
    source.close();
  });

  source.onerror = () => {
    handlers.onError("Live stream disconnected. Check backend logs and retry.");
    source.close();
  };

  return () => source.close();
}
