# Backend Scaffold

This backend is a controller-based .NET 10 Web API scaffold for the grocery store SOP assistant challenge.

## Endpoints

- `GET /api/health`
- `POST /api/ingest`
- `POST /api/chat`

## Intended Extension Points

- `Services/IChunkingService.cs`
- `Services/IEmbeddingService.cs`
- `Services/IVectorStoreService.cs`
- `Services/IRetrievalChatService.cs`
- `Services/IToolRegistryService.cs`

## Vector Store Artifact

The placeholder JSON artifact path is under `src/Api/Data/vector-store.json`.

The scaffold keeps that path in configuration, but the default service does not read, write, or search the artifact yet.

## Local SOP Path

The default local source path is `../../../../knowledge-base/Grocery_Store_SOP.md`, matching the API project's content root during local development.

Candidates are expected to replace the placeholder implementations with working ingest, embedding, retrieval, and chat logic.