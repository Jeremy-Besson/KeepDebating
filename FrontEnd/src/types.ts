export type DebateTurn = {
  round: number;
  speaker: string;
  stance: "PRO" | "CON" | string;
  message: string;
  turnKind: "argument" | "follow-up" | string;
  orchestratorReason?: string | null;
  toolFactsUsed: string[];
  timestamp: string;
};

export type HumanLoopCheckpoint = {
  checkpointId: string;
  reason: string;
  question: string;
  answer?: string | null;
  status: "pending" | "resolved" | string;
  askedAt: string;
  answeredAt?: string | null;
};

export type SpectatorVerdict = {
  spectatorName: string;
  perspective: string;
  winner: "PRO" | "CON" | "TIE" | string;
  confidence: number;
  rationale: string;
};

export type DebateTranscript = {
  topic: string;
  model: string;
  startedAt: string;
  completedAt: string;
  rounds: number;
  turns: DebateTurn[];
  humanLoopCheckpoints: HumanLoopCheckpoint[];
  spectatorVerdicts: SpectatorVerdict[];
};

export type DebateRunResponse = {
  transcript: DebateTranscript;
  summary: {
    finalWinner: "PRO" | "CON" | "TIE" | string;
    proVotes: number;
    conVotes: number;
    tieVotes: number;
  };
};

export type DebaterOption = {
  id: string;
  name: string;
  character: string;
};

export type DebateRunRequest = {
  topic: string;
  rounds: number;
  spectatorCount: number;
  proDebaterId: string;
  conDebaterId: string;
};

export type DebateStreamStarted = {
  sessionId: string;
  topic: string;
  rounds: number;
  spectatorCount: number;
  proDebater: DebaterOption;
  conDebater: DebaterOption;
};

export type DebateHumanQuestionEvent = {
  sessionId: string;
  checkpoint: HumanLoopCheckpoint;
};
