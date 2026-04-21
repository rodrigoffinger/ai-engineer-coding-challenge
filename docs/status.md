# Implementation Status

Last updated: 2026-04-21 — all 8 phases complete

---

## Summary

| Phase | Status |
|---|---|
| 0 — Setup & Verification | ✅ Complete |
| 1 — Chunking Service | ✅ Complete |
| 2 — Embedding Service | ✅ Complete |
| 3 — Vector Store Service | ✅ Complete |
| 4 — Ingest Endpoint | ✅ Complete |
| 5 — Tool Registry | ✅ Complete |
| 6 — Chat Service (RAG + Tools) | ✅ Complete |
| 7 — Frontend Polish | ✅ Complete |
| 8 — Integration Testing & Cleanup | ✅ Complete |

---

## Task Detail

### Phase 0 — Setup & Verification

| Task | Status | Notes |
|---|---|---|
| T0.1 — Verify local dev environment | ✅ | .NET 10.0.202, Node 20.16.0, backend builds and health endpoint runs |
| T0.2 — Add OpenAI NuGet package | ✅ | `OpenAI` v2.10.0 added to `backend/src/Api/Api.csproj`; installed with `--source nuget.org` due to unreachable corporate NuGet feed |
| T0.3 — Configure API key and models | ✅ | `appsettings.json` defaults: `gpt-4o-mini` / `text-embedding-3-small`; key in `appsettings.Development.json` (not committed) |

### Phase 1 — Chunking Service

| Task | Status | Notes |
|---|---|---|
| T1.1 — Extend `TextChunk` model | ✅ | Added `SectionTitle` property to `Models/TextChunk.cs` |
| T1.2 — Implement `MarkdownChunkingService` | ✅ | Splits on `##` / `###` headers; skips chunks with <20 chars of body; registered in DI |

### Phase 2 — Embedding Service

| Task | Status | Notes |
|---|---|---|
| T2.1 — Implement `OpenAiEmbeddingService` | ✅ | `EmbedAsync` + `EmbedBatchAsync` via `EmbeddingClient`; interface extended with `EmbedBatchAsync` |

### Phase 3 — Vector Store Service

| Task | Status | Notes |
|---|---|---|
| T3.1 — Implement full `FileVectorStoreService` | ✅ | Load/save via `System.Text.Json`; in-memory cache; cosine similarity linear scan |
| T3.2 — `VectorSearchMatch.Score` validation | ✅ | Field already existed in scaffold (`double Score`) |

### Phase 4 — Ingest Endpoint

| Task | Status | Notes |
|---|---|---|
| T4.1 — Implement full `IngestController` | ✅ | Resolves path relative to content root; honours `forceReingest`; zips chunks+embeddings into `VectorRecord[]`; stores section title in `Metadata["section"]` |

### Phase 5 — Tool Registry

| Task | Status | Notes |
|---|---|---|
| T5.1 — Define tool schemas and execution | ✅ | `search_sop` (embed query → vector search → return JSON) + `get_store_hours` (hardcoded from SOP §2.1); `IToolRegistryService` interface updated to use `ChatTool` from OpenAI SDK |

### Phase 6 — Chat Service

| Task | Status | Notes |
|---|---|---|
| T6.1 — Implement `RetrievalChatService` | ✅ | Pre-retrieves top-3 chunks → system prompt injection; single tool-calling round if model decides to use tools; citations from pre-retrieved chunks; `AssistantChatMessage(completion.Value)` used for tool round message injection (SDK v2 pattern) |

### Phase 7 — Frontend Polish

| Task | Status | Notes |
|---|---|---|
| T7.1 — Update initial assistant message | ✅ | Welcome message with ingest instruction |
| T7.2 — Show tool calls in transcript | ✅ | `toolCalls?: string[]` added to `ChatMessage` type; `🔧 tool_name` badges rendered in `ChatTranscript`; CSS added to `App.css` |
| T7.3 — Citation display | ✅ | `CitationsPanel` already functional; empty-state text updated |

### Phase 8 — Integration Testing & Cleanup

| Task | Status | Notes |
|---|---|---|
| T8.1 — End-to-end smoke test | ✅ | Validated via logs: ingest (76 chunks), chat with tool call (get_store_hours), chat with pure RAG, multi-turn context preserved |
| T8.2 — Remove placeholder flags | ✅ | All responses return `IsPlaceholder = false`; contracts kept for frontend compatibility |
| T8.3 — README / run instructions | ✅ | Created `docs/running.md` with setup, launch, and observability instructions |

---

## Decisions Made During Implementation

Beyond what was planned in `decisions.md`, one runtime decision was made:

**OpenAI NuGet source:** `dotnet add package OpenAI` failed due to an unreachable corporate NuGet feed (`nuget.digitaldesk.com.br`). Resolved by specifying `--source https://api.nuget.org/v3/index.json` explicitly. No config changes were made to `NuGet.Config`.

**SDK v2 tool-calling pattern:** When appending the model's first response to the message list before sending tool results, the correct type is `new AssistantChatMessage(completion.Value)` — not a direct cast from `ChatCompletion`. Compiler error caught this at build time.

---

## Files Created / Modified

### New files
| File | Purpose |
|---|---|
| `backend/src/Api/Services/MarkdownChunkingService.cs` | Section-based Markdown chunker |
| `backend/src/Api/Services/OpenAiEmbeddingService.cs` | OpenAI embeddings wrapper |
| `backend/src/Api/Services/ToolRegistryService.cs` | Tool schemas + execution dispatcher |
| `backend/src/Api/Services/RetrievalChatService.cs` | Full RAG + tool-calling chat flow |
| `start.bat` | Dev launcher (installs frontend deps, starts both servers) |
| `docs/decisions.md` | Technical decisions and tradeoffs |
| `docs/implementation-plan.md` | Task backlog |
| `docs/status.md` | This file |

### Modified files
| File | Change |
|---|---|
| `backend/src/Api/Api.csproj` | Added `OpenAI` v2.10.0 package reference |
| `backend/src/Api/Program.cs` | Replaced all placeholder DI registrations with real services |
| `backend/src/Api/Models/TextChunk.cs` | Added `SectionTitle` property |
| `backend/src/Api/Services/IEmbeddingService.cs` | Added `EmbedBatchAsync` |
| `backend/src/Api/Services/IToolRegistryService.cs` | Replaced `ToolDefinition` API with `ChatTool`-based API |
| `backend/src/Api/Services/FileVectorStoreService.cs` | Full implementation (was scaffold stub) |
| `backend/src/Api/Controllers/IngestController.cs` | Full implementation (was scaffold stub) |
| `backend/src/Api/appsettings.json` | Added default model names |
| `backend/src/Api/appsettings.Development.json` | Added `OpenAI:ApiKey` slot |
| `frontend/src/types/chat.ts` | Added `toolCalls?: string[]` to `ChatMessage` |
| `frontend/src/pages/ChatPage.tsx` | Updated initial message; propagates `toolCalls` to transcript |
| `frontend/src/components/ChatTranscript.tsx` | Renders tool call badges |
| `frontend/src/components/CitationsPanel.tsx` | Updated empty-state text |
| `frontend/src/App.css` | Added `.tool-calls` and `.tool-badge` styles |

### Deleted files
| File | Reason |
|---|---|
| `backend/src/Api/Services/PlaceholderChunkingService.cs` | Replaced by `MarkdownChunkingService` |
| `backend/src/Api/Services/PlaceholderEmbeddingService.cs` | Replaced by `OpenAiEmbeddingService` |
| `backend/src/Api/Services/PlaceholderToolRegistryService.cs` | Replaced by `ToolRegistryService` |
| `backend/src/Api/Services/PlaceholderRetrievalChatService.cs` | Replaced by `RetrievalChatService` |
