# Frontend Scaffold

This frontend is a minimal React + TypeScript chat UI scaffold for the grocery store SOP assistant challenge.

## What Is Included

- Transcript area with scroll behavior
- Composer with disabled and loading states
- Ingest trigger wired to the backend placeholder endpoint
- Status banner for health, ingest, and chat feedback
- Citations panel hook-up for backend responses
- Typed API client

The ingest input is prefilled with `../../../../knowledge-base/Grocery_Store_SOP.md` so it matches the backend scaffold's local default.

## Run Locally

```powershell
npm install
npm run dev
```

By default the app calls `http://localhost:5181`.

To override the API base URL:

```powershell
$env:VITE_API_BASE_URL = "http://localhost:5181"
npm run dev
```

The UI is intentionally light. Candidates are expected to extend behavior rather than redesign the scaffold from scratch.
