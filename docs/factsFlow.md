# Facts Flow

How facts are gathered, stored, and injected into the debate.

---

## Overview

Two complementary mechanisms supply facts to debaters:

| Mechanism | Trigger | Query type | Data source |
|---|---|---|---|
| **Wikipedia tool** | Brain explicitly decides to call it | Specific keyword query | Live HTTP call |
| **RAG retrieval** | Automatic before every debater turn | Semantic similarity | In-memory vector store |

Wikipedia feeds RAG: every article fetched by the tool is embedded and stored, growing the knowledge base as the debate progresses.

---

## Step-by-step flow

### 1. Brain decides to call Wikipedia

During `DebateBrainOrchestrator.DecideAsync`, the LLM receives the full debate state (topic, round, recent turns) and autonomously decides whether to call the Wikipedia tool and what query to use.

```
Debate state JSON
    → LLM with FunctionChoiceBehavior.Auto()
    → calls Wikipedia.search("specific query tailored to current argument")
```

The query is generated fresh every time by the LLM — it is never hard-coded.

---

### 2. Wikipedia search (two-step HTTP)

`WikipediaPlugin.SearchAsync` runs two sequential HTTP calls:

```
Step 1 — Find best article title
GET https://en.wikipedia.org/w/api.php
    ?action=query&list=search&srsearch={query}&srlimit=3
    → picks the first result title

Step 2 — Fetch article summary
GET https://en.wikipedia.org/api/rest_v1/page/summary/{title}
    → extracts: title, extract (capped at 500 chars), canonical URL

Result returned to the LLM:
"Wikipedia: {title}. {extract}... Source: {url}"
```

---

### 3. Wikipedia cache

Before any HTTP call, the plugin checks a per-session `Dictionary<string, string>` keyed on the lowercased query.

```
Incoming query
    → normalize to lowercase
    → cache hit?  → return cached string immediately (no HTTP)
    → cache miss? → fetch from Wikipedia → store in cache → continue
```

The cache lives for the duration of the debate session. It prevents redundant HTTP calls when the brain searches for the same topic multiple times.

---

### 4. RAG indexing (Wikipedia → vector store)

After a successful Wikipedia fetch and cache write, the result is also embedded and stored in `DebateKnowledgeStore`:

```
Wikipedia result string
    → POST to Azure OpenAI text-embedding-3-small
    → returns float[1536]  (a vector encoding the meaning)
    → stored as KnowledgeEntry { Id, Text, Embedding }
      in an in-memory List<KnowledgeEntry>
```

This step is non-fatal: if the embedding API fails, a warning is logged and the Wikipedia result is still returned to the brain normally.

The vector store grows entry by entry as the debate progresses. Early turns are Wikipedia-heavy; later turns benefit from accumulated knowledge without new HTTP calls.

---

### 5. Brain returns facts to the orchestrator

The LLM places the Wikipedia facts it used into `retrievedFacts[]` inside the `BrainDecision` JSON it returns:

```json
{
  "decision": "next-turn",
  "speakerStance": "PRO",
  "turnKind": "argument",
  "retrievedFacts": [
    "Wikipedia: Tamagotchi. A Tamagotchi is a handheld digital pet... Source: https://..."
  ]
}
```

These facts are carried forward by `DebateOrchestrator`.

---

### 6. RAG retrieval (before each debater turn)

Before generating each debater turn, `DebateOrchestrator` queries the knowledge store:

```
RAG query = "{debate topic} + {last turn message}"
    → POST to Azure OpenAI text-embedding-3-small
    → returns float[1536] for the query
    → cosine similarity computed against every stored entry
    → top-2 most relevant entries returned as strings
```

This retrieval is automatic and implicit — nobody specifies what to search for. The system finds what is semantically closest to the current argument context.

This step is also non-fatal: if the embedding API fails, a warning is logged and the turn continues with only the brain's `retrievedFacts`.

---

### 7. Fact merging

The brain's `retrievedFacts` and the RAG results are merged and deduplicated:

```
brain.RetrievedFacts  (explicit Wikipedia facts chosen by the LLM)
    + ragFacts        (implicit top-2 semantically similar entries)
    → Distinct()      (case-insensitive deduplication)
    → toolFacts[]
```

If the merged list is empty (no Wikipedia call was made yet and the store is empty), a fallback placeholder is used: `"No fact was retrieved for this turn."`.

---

### 8. Facts injected into the debater prompt

`DebateOrchestrator.BuildUserPrompt` injects `toolFacts` as a bullet list into the debater's user prompt:

```
Your argument this turn must be built around this fact:
- Wikipedia: Tamagotchi. A Tamagotchi is a handheld digital pet...
- Wikipedia: Child development. Children benefit from...

Instructions:
- The provided fact is your only source of new claims.
- Do not introduce arguments not grounded in it.
```

The debater is constrained to argue only from the provided facts, keeping arguments grounded and traceable.

---

## Logging reference

| Event ID | Logger | Message | When |
|---|---|---|---|
| 1100 | WikipediaPlugin | `Wikipedia tool search started. Query: {Query}` | Every search attempt |
| 1101 | WikipediaPlugin | `Wikipedia tool search completed. Query: {Query}. Response: {Response}` | Successful fetch |
| 1102 | WikipediaPlugin | `Wikipedia tool search cache hit. Query: {Query}` | Cache hit |
| 1103 | WikipediaPlugin | `Wikipedia tool search failed. Query: {Query}. ExceptionType: {ExceptionType}` | HTTP or parse error |
| 1104 | WikipediaPlugin | `Wikipedia tool search telemetry. Query: {Query}. DurationMs: {DurationMs}` | Always (finally block) |
| 1105 | WikipediaPlugin | `Knowledge store embedding failed. Query: {Query}. ExceptionType: {ExceptionType}` | Embedding API error |
| 1200 | DebateKnowledgeStore | `Knowledge store: stored entry. Id: {Id}. Total entries: {Count}` | After successful embed+store |
| 1201 | DebateKnowledgeStore | `Knowledge store: retrieved top-{TopK} results for query: {Query}. Results: {Results}` | After each RAG retrieval |
| 1202 | DebateKnowledgeStore | `Knowledge store: skipped duplicate entry. Id: {Id}` | Duplicate query ignored |
| 2004 | DebateOrchestrator | `Turn facts injected. Round: {Round}, Stance: {Stance}, Facts: {Facts}` | Before each debater turn |
| 2005 | DebateOrchestrator | `RAG retrieval failed. ExceptionType: {ExceptionType}` | RAG embedding API error |

---

## End-to-end sequence per turn

```
DebateOrchestrator.ContinueDebateAsync()
    │
    ├─ DebateBrainOrchestrator.DecideAsync()
    │       │
    │       ├─ [optional] WikipediaPlugin.SearchAsync("query")
    │       │       ├─ cache hit? → return cached string
    │       │       ├─ cache miss → Wikipedia Search API → get title
    │       │       │              → Wikipedia Summary API → get extract
    │       │       │              → store in cache
    │       │       └─ DebateKnowledgeStore.StoreAsync()
    │       │               → embed text via text-embedding-3-small
    │       │               → store float[1536] in List<KnowledgeEntry>
    │       │
    │       └─ returns BrainDecision { retrievedFacts[] }
    │
    ├─ DebateKnowledgeStore.RetrieveAsync(topic + lastTurnMessage)
    │       → embed query via text-embedding-3-small
    │       → cosine similarity against all stored entries
    │       → return top-2 texts
    │
    ├─ merge retrievedFacts + ragFacts → toolFacts[]
    │
    └─ GenerateTurn(speaker, toolFacts)
            → BuildUserPrompt() injects toolFacts as bullet list
            → LLM generates debater speech grounded in facts
```
