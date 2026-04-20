import type { FormEvent } from 'react'

interface ChatComposerProps {
  value: string
  onChange: (value: string) => void
  onSubmit: () => void
  isBusy: boolean
}

export function ChatComposer({ value, onChange, onSubmit, isBusy }: ChatComposerProps) {
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit()
  }

  return (
    <form className="composer" onSubmit={handleSubmit}>
      <label htmlFor="chat-input">Ask about the grocery store SOP</label>
      <textarea
        id="chat-input"
        rows={4}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder="Example: What are the opening checklist steps for the manager on duty?"
        disabled={isBusy}
      />
      <div className="composer-actions">
        <span className="hint">The backend currently returns an explicit not-implemented placeholder for chat.</span>
        <button className="primary-button" type="submit" disabled={isBusy || value.trim().length === 0}>
          {isBusy ? 'Sending...' : 'Send'}
        </button>
      </div>
    </form>
  )
}