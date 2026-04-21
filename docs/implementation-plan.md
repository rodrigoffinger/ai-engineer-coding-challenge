# Implementation Plan

## Overview

Build a working vertical slice of a Grocery Store SOP Assistant: document ingestion → chunking → embedding → vector store → RAG + tool-calling chat → React UI.

Stack: C# / .NET 10 Web API (backend) · React + TypeScript (frontend) · OpenAI API (AI services).

All technical rationale is documented in [`decisions.md`](./decisions.md).

---

## High-Level Architecture

```
Frontend (React)
  ├── IngestPanel  → POST /api/ingest
  └── ChatPage     → POST /api/chat

Backend (.NET 10)
  ├── IngestController
  │     ├── MarkdownChunkingService      — splits SOP by ### headers
  │     ├── OpenAiEmbeddingService       — text-embedding-3-small
  │     └── FileVectorStoreService       — List<VectorRecord> ↔ JSON file
  │
  └── ChatController
        └── RetrievalChatService
              ├── embed query → cosine search → top-3 chunks
              ├── build system prompt with retrieved context
              ├── call gpt-4o-mini with tools (search_sop, get_store_hours)
              ├── if tool_call → execute → re-call model
              └── return AssistantMessage + Citations
```

---

## Backlog

Tasks are ordered by dependency. Each task maps to a single coherent unit of work.

---

### Phase 0 — Setup & Verification

#### T0.1 — Verify local dev environment
- Confirm `dotnet --version` ≥ 10 and backend builds/runs (`dotnet run`)
- Confirm `node --version` and frontend runs (`npm run dev`)
- Confirm backend health endpoint responds at `http://localhost:5181/api/health`
- **Done when:** Both processes start without errors and health check returns 200.

#### T0.2 — Add OpenAI NuGet package
- `dotnet add package OpenAI` (official SDK, v2.x) in `backend/src/Api/`
- **Done when:** `dotnet build` succeeds with the package referenced.

#### T0.3 — Configure API key and model names
- Add `OpenAI__ApiKey`, `OpenAI__ChatModel` (`gpt-4o-mini`), `OpenAI__EmbeddingModel` (`text-embedding-3-small`) to `appsettings.Development.json` (not committed) or user secrets
- Update `appsettings.json` with placeholder keys and model name defaults
- **Done when:** Configuration is readable in the DI container.

---

### Phase 1 — Chunking Service

#### T1.1 — Extend `TextChunk` model
- Add `string SectionTitle` property to `Models/TextChunk.cs`
- **Done when:** Model compiles.

#### T1.2 — Implement `MarkdownChunkingService`
- File: `Services/MarkdownChunkingService.cs` implementing `IChunkingService`
- Algorithm:
  1. Split input text on lines
  2. Walk lines; on a `##` or `###` header, start a new chunk
  3. Accumulate body lines until the next header
  4. Emit a `TextChunk` per header block with `SectionTitle = header text`, `Source = sourceName`, `Content = header + body text`
  5. Skip chunks with fewer than 20 characters of body content (e.g. TOC lines)
- Register in `Program.cs` replacing `PlaceholderChunkingService`
- **Done when:** A unit test (or manual log) on `Grocery_Store_SOP.md` produces ~50–80 chunks with meaningful titles.

---

### Phase 2 — Embedding Service

#### T2.1 — Implement `OpenAiEmbeddingService`
- File: `Services/OpenAiEmbeddingService.cs` implementing `IEmbeddingService`
- Interface to define (add to `IEmbeddingService.cs`):
  ```csharp
  Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
  Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default);
  ```
- Uses `OpenAI.Embeddings.EmbeddingClient` from the SDK
- Reads model name from `IConfiguration["OpenAI:EmbeddingModel"]`
- Register in `Program.cs` replacing `PlaceholderEmbeddingService`
- **Done when:** Calling `EmbedAsync("hello world")` returns a non-empty `float[]`.

---

### Phase 3 — Vector Store Service

#### T3.1 — Implement full `FileVectorStoreService`
- File: `Services/FileVectorStoreService.cs` (replace scaffold placeholder methods)
- **`SaveAsync`:** Serialize `IEnumerable<VectorRecord>` to JSON at `_artifactPath` using `System.Text.Json`; create directory if missing.
- **`LoadAsync`:** Deserialize from JSON file; return empty list if file does not exist.
- **`SearchAsync`:** 
  1. Load records (or use in-memory cache loaded at startup)
  2. For each record, compute cosine similarity between `queryEmbedding` and `record.Embedding`
  3. Return top-K as `VectorSearchMatch` objects sorted descending by score
- In-memory cache: inject as a singleton field; populate on first `LoadAsync` or after `SaveAsync`
- **Done when:** After ingest, `vector-store.json` contains records with non-zero embeddings; `SearchAsync` returns relevant chunks for a test query.

#### T3.2 — Add `VectorSearchMatch` score field validation
- Confirm `Models/VectorSearchMatch.cs` has a `Score` (float) property; add if missing.
- **Done when:** Compiles.

---

### Phase 4 — Ingest Endpoint

#### T4.1 — Implement full `IngestController`
- Replace placeholder logic in `Controllers/IngestController.cs`
- Flow:
  1. Resolve source path (request body or config)
  2. If `forceReingest == false` and vector store file exists with records → return early with existing count
  3. Read file content from disk
  4. Call `IChunkingService.ChunkAsync`
  5. Call `IEmbeddingService.EmbedBatchAsync` on all chunk texts
  6. Zip chunks + embeddings into `VectorRecord[]`, populate `Metadata["section"]` from `SectionTitle`
  7. Call `IVectorStoreService.SaveAsync`
  8. Return `IngestResponse` with real counts and `IsPlaceholder = false`
- Wire up `IChunkingService`, `IEmbeddingService`, `IVectorStoreService` via constructor injection
- **Done when:** Hitting "Ingest" in the UI returns `chunksCreated > 0` and `vector-store.json` is written to disk.

---

### Phase 5 — Tool Registry

#### T5.1 — Define tool schemas and execution

- File: `Services/ToolRegistryService.cs` implementing `IToolRegistryService`
- Interface update (add to `IToolRegistryService.cs`):
  ```csharp
  IReadOnlyList<ChatTool> GetTools();
  Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken ct = default);
  ```
- **`get_store_hours`:**
  - No parameters
  - Returns a JSON string with the store hours table from SOP §2.1 (hardcoded)
- **`search_sop`:**
  - Parameter: `query` (string), `top_k` (int, default 3)
  - Embeds the query, calls `IVectorStoreService.SearchAsync`, returns serialized list of `{ section, snippet }` objects
- Register in `Program.cs` replacing `PlaceholderToolRegistryService`
- **Done when:** Both tools execute without error when called directly.

---

### Phase 6 — Chat Service (RAG + Tool-Calling)

#### T6.1 — Implement `RetrievalChatService`
- File: `Services/RetrievalChatService.cs` implementing `IRetrievalChatService`
- Flow:
  ```
  1. Extract last user message text
  2. Embed it → cosine search → top-3 VectorSearchMatches
  3. Build system prompt:
       "You are a helpful assistant for grocery store employees. 
        Answer questions based on the SOP document.
        Use only the provided context and tool results.
        If the answer is not in the context, say so.
        
        Context:
        [chunk 1 — section title]
        [chunk text]
        ...
  4. Map request.Messages → OpenAI ChatMessage list
  5. Prepend system message
  6. Call ChatClient.CompleteChatAsync(messages, tools, options)
  7. If response.FinishReason == ToolCalls:
       a. For each tool call: execute via IToolRegistryService
       b. Append ToolChatMessage with result
       c. Re-call ChatClient.CompleteChatAsync → final response
  8. Build CitationDto list from the top-3 pre-retrieved chunks
  9. Return ChatResponse { AssistantMessage, Citations, Status = "ok", IsPlaceholder = false }
  ```
- Uses `OpenAI.Chat.ChatClient` from the SDK
- Reads model name from `IConfiguration["OpenAI:ChatModel"]`
- Register in `Program.cs` replacing `PlaceholderRetrievalChatService`
- **Done when:** Sending a question about food safety temperatures returns a grounded answer with citations.

---

### Phase 7 — Frontend Polish

#### T7.1 — Update initial assistant message
- Replace scaffold placeholder text in `ChatPage.tsx` initial messages array with a real welcome message explaining what the assistant does.
- **Done when:** Fresh page load shows a meaningful greeting.

#### T7.2 — Show tool calls in transcript
- `ChatResponse.toolCalls` is `List<string>` (tool names invoked)
- In `ChatTranscript.tsx`, render a small badge/note under assistant messages that triggered tool calls (e.g. "Used tool: get_store_hours")
- Update `ChatResponse` contract on backend to populate `ToolCalls` with invoked tool names
- **Done when:** Asking "what are the store hours?" shows a tool call badge in the UI.

#### T7.3 — Citation display
- `CitationsPanel.tsx` already exists; verify it renders `source` (section name) and `snippet` fields clearly
- Minor: truncate snippet to 120 characters with ellipsis if needed
- **Done when:** Citations panel shows section titles and snippets after a chat response.

---

### Phase 8 — Integration Testing & Cleanup

#### T8.1 — End-to-end smoke test
- Start backend + frontend
- Click Ingest → verify `vector-store.json` written, chunk count shown
- Ask "What are the store hours?" → verify tool call used + correct answer
- Ask "What temperature should poultry be cooked to?" → verify RAG answer + citations
- Ask a follow-up ("What about ground beef?") → verify multi-turn context preserved
- **Done when:** All three scenarios work correctly.

#### T8.2 — Remove placeholder flags
- Ensure no response returns `IsPlaceholder = true` from implemented services
- Remove or repurpose `IsPlaceholder` fields from services (keep in contracts for UI compatibility)
- **Done when:** UI status banner no longer shows "warning" state after successful operations.

#### T8.3 — README / run instructions
- Update root `README.md` (or create `docs/running.md`) with exact commands to run backend and frontend locally
- Document where to put the OpenAI API key (`appsettings.Development.json` or env var)
- **Done when:** A fresh clone can run the app following only the documented steps.

---

## Task Dependency Graph

```
T0.1 → T0.2 → T0.3
                 ↓
              T1.1 → T1.2
                         ↓
              T2.1 ──────→ T4.1
                         ↑
              T3.1 → T3.2 ┘
                         ↓
              T5.1 ──────→ T6.1
                               ↓
              T7.1, T7.2, T7.3 ┘
                                ↓
                             T8.1 → T8.2 → T8.3
```

---

## Estimated Effort

| Phase | Tasks | Notes |
|---|---|---|
| 0 — Setup | T0.1–T0.3 | ~15 min |
| 1 — Chunking | T1.1–T1.2 | ~20 min |
| 2 — Embedding | T2.1 | ~15 min |
| 3 — Vector Store | T3.1–T3.2 | ~25 min |
| 4 — Ingest endpoint | T4.1 | ~20 min |
| 5 — Tool registry | T5.1 | ~20 min |
| 6 — Chat service | T6.1 | ~40 min (core complexity) |
| 7 — Frontend | T7.1–T7.3 | ~20 min |
| 8 — Polish | T8.1–T8.3 | ~20 min |
| **Total** | | **~3 hours** |

---

## Out of Scope (deliberate)

- Authentication / authorization
- External vector database
- Production deployment / Docker
- Unit test suite (though T1.2 verification is manual-test driven)
- Multi-document ingestion
- History windowing / token budget enforcement (acceptable for POC scale)
- Streaming chat responses (adds frontend complexity with marginal POC benefit)
