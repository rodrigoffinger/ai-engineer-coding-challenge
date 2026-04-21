# Technical Decisions

## Context

This document records every significant technical decision made before and during implementation of the Grocery Store SOP Assistant coding challenge. For each decision the alternatives considered and their tradeoffs are listed so that the reasoning behind each choice is transparent to evaluators.

---

## D1 — AI Provider / API

**Decision:** OpenAI (as supplied via the challenge-provided API key).

| Alternative | Pros | Cons |
|---|---|---|
| OpenAI (chosen) | Key provided; function-calling is mature; .NET SDK is first-class | External dependency; paid |
| Anthropic Claude | Strong instruction-following | No key provided; tool-use API differs |
| Azure OpenAI | Enterprise-grade | Requires Azure subscription + deployment setup |
| Local model (Ollama) | Free; no network | Embedding quality lower; setup complexity; no function-calling parity |

**Models targeted (configurable via `appsettings.json`):**
- Chat: `gpt-4o-mini` — cheaper and fast, sufficient for RAG over a small SOP document. Fallback to `gpt-4o` if the key has access.
- Embeddings: `text-embedding-3-small` — OpenAI's most cost-efficient embedding model, 1536 dimensions, strong retrieval quality.

---

## D2 — OpenAI Integration Library

**Decision:** Raw `OpenAI` NuGet package (official OpenAI .NET SDK, v2.x).

| Alternative | Pros | Cons |
|---|---|---|
| Raw OpenAI SDK (chosen) | Explicit; no magic; evaluator can trace every call; minimal dependencies | Slightly more boilerplate for tool dispatch |
| Semantic Kernel | Microsoft-standard; plugin system; future-proof | Abstracts tool-calling behind conventions that obscure the mechanics; heavier dependency tree; overkill for a POC |
| Microsoft Agent Framework | Richer agent loop support | Even heavier; not needed for single-turn tool-use |
| Manual HTTP (HttpClient) | Zero extra deps | Much more boilerplate; error-prone |

**Why this matters to the evaluator:** The challenge asks for evidence that we understand how to *design tool schemas, let the model select tools, execute them, and feed results back*. A raw SDK keeps that flow explicit and readable.

---

## D3 — Chunking Strategy

**Decision:** Section-based Markdown chunking — each `###` subsection becomes one chunk; `##` top-level sections without subsections also become chunks.

| Alternative | Pros | Cons |
|---|---|---|
| Section-based / header-aware (chosen) | Chunks are semantically coherent; section title becomes a natural citation; no overlap needed; fits this document perfectly | Chunk sizes vary; not generic across arbitrary documents |
| Fixed-size sliding window (e.g. 512 tokens, 64 overlap) | Generic; predictable token budget per chunk | Splits tables and procedures mid-sentence; requires a tokenizer dependency (tiktoken) |
| Paragraph-based | Simple; moderate coherence | Section context lost without metadata; many tiny chunks for table-heavy sections |
| Full document as single chunk | Zero implementation effort | Exceeds context window; no retrieval precision; violates challenge requirements |

**Implementation note:** The section title is stored in `VectorRecord.Metadata["section"]` and reflected back as the `Citation.Source`. The raw chunk text is stored in `VectorRecord.ChunkText`.

**TextChunk model extension:** `TextChunk` is extended with a `SectionTitle` property (not in the original scaffold) to carry the header text through the chunking pipeline cleanly.

---

## D4 — Vector Store (In-Memory + JSON Persistence)

**Decision:** In-memory `List<VectorRecord>` loaded at startup from `Data/vector-store.json`; cosine similarity computed in-memory at query time; full list serialized back to JSON on every ingest.

| Alternative | Pros | Cons |
|---|---|---|
| In-memory + JSON (chosen) | Meets challenge constraints exactly; zero external deps; fast for ~100 chunks | Not scalable beyond tens of thousands of records |
| SQLite with vector extension | Persistent; portable single file | External dep (Microsoft.Data.Sqlite or sqlite-vec); more setup |
| Qdrant / Weaviate / Pinecone | Production-grade; ANN search | Violates "no external vector DB" constraint |
| Redis + RediSearch | Fast; familiar | External service; setup friction |

**Cosine similarity:** For ~100 chunks (estimated from the SOP's ~20 sections × ~3 subsections each) an O(n) linear scan computing dot products is negligible. No ANN index is needed.

**Serialization:** `System.Text.Json` (already in .NET 10 BCL) — no extra dependency.

---

## D5 — Tool Design

**Decision:** Two tools registered via OpenAI function-calling.

### Tool 1 — `search_sop`
```json
{
  "name": "search_sop",
  "description": "Search the grocery store SOP document for policy or procedure details matching a query. Returns the most relevant text chunks and their section sources.",
  "parameters": {
    "query": { "type": "string", "description": "The search query" },
    "top_k": { "type": "integer", "description": "Number of results to return (default 3)" }
  }
}
```
*Purpose:* Demonstrates that the model can initiate a retrieval step as a tool call (beyond the pre-seeded RAG context in the system prompt). Gives the model agency over when and what to search.

### Tool 2 — `get_store_hours`
```json
{
  "name": "get_store_hours",
  "description": "Returns the store's operating hours for each day of the week.",
  "parameters": {}
}
```
*Purpose:* A lightweight, hardcoded tool (data sourced from SOP §2.1) that demonstrates tool-use independent of the vector search path. Forces the model to *decide* between direct answer, RAG context, or a dedicated tool — which is the core competency being evaluated.

**Why two tools vs. one:** One tool alone could be seen as trivially wired. Two tools with different parameter shapes and execution paths more clearly demonstrate schema design, dispatch logic, and result injection.

**What was not built:**
- `lookup_policy(section)` — redundant with `search_sop`
- `log_incident()` — write tools add complexity with no POC benefit
- Multi-step agent loop — explicitly out of scope per challenge FAQ

---

## D6 — RAG + Tool-Calling Chat Flow

**Decision:** Hybrid approach — pre-retrieve top-3 chunks and inject them into the system prompt *and* expose `search_sop` as a callable tool.

```
User message →
  1. Embed user query (text-embedding-3-small)
  2. Search vector store → top-3 chunks (cosine similarity)
  3. Build system prompt:
       - Role + grounding instructions
       - Retrieved context (chunks + section names)
  4. Call OpenAI chat completions with:
       - Full conversation history (messages[])
       - System prompt
       - Tool definitions
  5. If model returns a tool_call:
       a. Execute the tool
       b. Append tool result message
       c. Call OpenAI again → final response
  6. Return AssistantMessage + Citations (from pre-retrieved chunks)
```

| Alternative | Pros | Cons |
|---|---|---|
| Hybrid pre-RAG + tools (chosen) | Model always has baseline context; also has agency to search more; clean demonstration of both RAG and tool-calling | Slightly more tokens per request |
| Tool-only (no pre-RAG) | Cleaner separation; pure tool-calling demonstration | Responses degrade if model doesn't call search_sop; citations only available if tool was called |
| Pre-RAG only (no tools) | Simpler; fewer round-trips | Doesn't demonstrate tool-calling at all |
| Agentic loop (multiple tool rounds) | More powerful | Out of scope per challenge; increases latency and token cost |

**Citation strategy:** Citations are populated from the pre-retrieved chunks (step 2), regardless of whether the model called `search_sop` additionally. This ensures citations are always present and traceable.

---

## D7 — Conversation State

**Decision:** Stateless server — the frontend sends the full message history (`messages[]`) with every request.

| Alternative | Pros | Cons |
|---|---|---|
| Stateless / client-carries-history (chosen) | Simple; no server-side state management; OpenAI-idiomatic; scales trivially | Payload grows with conversation length; history truncation must be handled client-side or backend |
| Server-side in-memory store by `conversationId` | Cleaner API (send only new message); server controls history trimming | Needs a `ConcurrentDictionary<string, List<ChatMessage>>`; state lost on restart; locks if multi-instance |
| Server-side persisted (DB/file) | Survives restarts | Far beyond POC scope |

**History management:** The chat service will pass the full `messages[]` array to OpenAI. For a POC conversation over a single SOP document this is fine; in production we would window to the last N tokens.

---

## D8 — Frontend Approach

**Decision:** Keep and extend the existing React + TypeScript scaffold. No additional UI component libraries.

| Alternative | Pros | Cons |
|---|---|---|
| Keep scaffold + extend (chosen) | Faster; evaluator can see that we read and understood the scaffold; no churn | Limited visual polish |
| Start fresh with shadcn/ui or Chakra | More polished | Time cost; diverges from challenge scaffold for no functional gain |
| Start fresh minimal | Full control | Rewrites working code for no reason |

**Changes needed in the frontend:**
- Update the initial assistant message (remove "scaffold placeholder" text)
- Add tool call display in the transcript (show which tools were invoked)
- Minor citation display improvements (section name + snippet)
- No structural changes to `ChatPage`, `apiClient`, or `types/chat`

---

## D9 — Ingest Idempotency

**Decision:** `forceReingest: boolean` flag (already in `IngestRequest`) — if `false` and a non-empty vector store already exists on disk, skip re-embedding and return the existing record count. If `true`, always re-chunk and re-embed.

**Why:** Embeddings cost tokens. Re-ingesting the same document on every dev restart is wasteful. The frontend already sends this flag; we just need to honour it.

---

## Summary Table

| # | Decision | Chosen Option |
|---|---|---|
| D1 | AI Provider | OpenAI |
| D2 | .NET Integration Library | Raw `OpenAI` NuGet SDK v2 |
| D3 | Chunking Strategy | Section-based (Markdown `###` headers) |
| D4 | Vector Store | In-memory List + JSON file, cosine similarity |
| D5 | Tools | `search_sop` + `get_store_hours` |
| D6 | Chat Flow | Hybrid: pre-RAG inject + tool-calling single round |
| D7 | Conversation State | Stateless (client carries full history) |
| D8 | Frontend | Extend existing scaffold |
| D9 | Ingest Idempotency | `forceReingest` flag, skip if store exists |
