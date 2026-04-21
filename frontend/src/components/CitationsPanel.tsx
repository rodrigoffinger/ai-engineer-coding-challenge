import type { Citation } from '../types/chat'

interface CitationsPanelProps {
  citations: Citation[]
}

export function CitationsPanel({ citations }: CitationsPanelProps) {
  return (
    <section className="sidebar-card" aria-labelledby="citations-heading">
      <h2 id="citations-heading">Citations</h2>
      {citations.length === 0 ? (
        <p className="empty-state">No citations yet. Send a message to see which SOP sections were used to answer.</p>
      ) : (
        <ul className="citations-list">
          {citations.map((citation, index) => (
            <li key={`${citation.source}-${index}`} className="citation-item">
              <p className="citation-source">
                {citation.source}
                {citation.startLine ? ` (${citation.startLine}-${citation.endLine ?? citation.startLine})` : ''}
              </p>
              <p>{citation.snippet}</p>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}