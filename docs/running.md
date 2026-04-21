# Running the Application

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 10.0+ |
| Node.js | 20.19+ or 22.12+ |
| OpenAI API key | with access to `gpt-4o-mini` and `text-embedding-3-small` |

---

## 1. Configure the OpenAI API Key

Create (or edit) `backend/src/Api/appsettings.Development.json` and add your key:

```json
{
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}
```

This file is not committed. The model names default to `gpt-4o-mini` and `text-embedding-3-small` and can be overridden in the same file:

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "ChatModel": "gpt-4o",
    "EmbeddingModel": "text-embedding-3-small"
  }
}
```

---

## 2. Start Both Services

From the repository root, run:

```
start.bat
```

This will:
1. Run `npm install` in the `frontend/` directory
2. Open a terminal window for the backend at `http://localhost:5181`
3. Open a terminal window for the frontend at `http://localhost:5173`

### Manual alternative

**Backend:**
```bash
cd backend/src/Api
dotnet run --launch-profile http
```

**Frontend** (separate terminal):
```bash
cd frontend
npm install
npm run dev
```

---

## 3. Use the App

1. Open `http://localhost:5173` in your browser
2. Click **Run Ingest** in the sidebar — this reads the SOP, chunks it, generates embeddings, and saves them to `backend/src/Api/Data/vector-store.json`. Takes ~3 seconds.
3. Start chatting. The assistant answers questions about the SOP with citations.

> Ingest only needs to run once. On subsequent starts the vector store is loaded from disk automatically and the **Run Ingest** button is a no-op unless **Force Reingest** is enabled.

---

## Observability

The backend logs to the console and to **Chainsaw** via UDP on port `9998` (log4j XML format).

To use Chainsaw:
1. Download [Apache Chainsaw](https://logging.apache.org/chainsaw/)
2. Configure a UDP receiver on port `9998`
3. Start the backend — log events will stream in real time

Log levels:
- `INF` — normal operation (requests, chunk counts, token usage)
- `DBG` — verbose detail (cache hits, per-chunk scores, individual embed calls)
- `WRN` — recoverable issues (empty vector store on search)
- `ERR` — failures (file not found, OpenAI errors, tool execution failures)

Token usage is logged after every OpenAI call with per-request and running session totals.

---

## Project Structure

```
├── backend/
│   └── src/Api/
│       ├── Controllers/          # IngestController, ChatController, HealthController
│       ├── Services/             # Chunking, Embedding, VectorStore, Tools, Chat
│       ├── Models/               # TextChunk, VectorRecord, VectorSearchMatch
│       ├── Contracts/            # DTOs for HTTP request/response
│       └── Data/vector-store.json  # Generated on first ingest
├── frontend/
│   └── src/
│       ├── pages/ChatPage.tsx
│       ├── components/           # ChatTranscript, CitationsPanel, IngestPanel, ...
│       ├── services/apiClient.ts
│       └── types/chat.ts
├── knowledge-base/
│   └── Grocery_Store_SOP.md      # Source document (20 sections, 76 chunks)
├── docs/
│   ├── decisions.md              # Technical decisions and tradeoffs
│   ├── implementation-plan.md    # Task backlog
│   ├── status.md                 # Execution status
│   └── running.md                # This file
└── start.bat                     # Dev launcher
```
