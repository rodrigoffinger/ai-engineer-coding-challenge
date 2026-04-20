interface IngestPanelProps {
  sourcePath: string
  onSourcePathChange: (value: string) => void
  onIngest: () => void
  isBusy: boolean
}

export function IngestPanel({ sourcePath, onSourcePathChange, onIngest, isBusy }: IngestPanelProps) {
  return (
    <section className="sidebar-card" aria-labelledby="ingest-heading">
      <h2 id="ingest-heading">Ingest</h2>
      <p>Use the backend-ready default path for the provided SOP and trigger the placeholder ingest endpoint.</p>
      <label htmlFor="source-path">Source document path</label>
      <input
        id="source-path"
        className="source-input"
        value={sourcePath}
        onChange={(event) => onSourcePathChange(event.target.value)}
        disabled={isBusy}
      />
      <div className="ingest-actions">
        <span className="hint">Default local path: ../../../../knowledge-base/Grocery_Store_SOP.md</span>
        <button className="secondary-button" type="button" onClick={onIngest} disabled={isBusy || sourcePath.trim().length === 0}>
          {isBusy ? 'Ingesting...' : 'Run Ingest'}
        </button>
      </div>
    </section>
  )
}