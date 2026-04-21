import { useEffect, useRef } from 'react'
import type { ChatMessage } from '../types/chat'

interface ChatTranscriptProps {
  messages: ChatMessage[]
}

function formatTimestamp(timestamp: string) {
  return new Date(timestamp).toLocaleTimeString([], {
    hour: 'numeric',
    minute: '2-digit',
  })
}

export function ChatTranscript({ messages }: ChatTranscriptProps) {
  const endOfMessagesRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    endOfMessagesRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [messages])

  return (
    <section className="chat-transcript" aria-label="Chat transcript" aria-live="polite">
      {messages.map((message) => (
        <article key={message.id} className="message-card" data-role={message.role}>
          <div className="message-meta">
            <strong>{message.role === 'assistant' ? 'Assistant' : 'You'}</strong>
            <span>{formatTimestamp(message.timestamp)}</span>
          </div>
          <p className="message-body">{message.content}</p>
          {message.toolCalls && message.toolCalls.length > 0 && (
            <div className="tool-calls">
              {message.toolCalls.map((tool) => (
                <span key={tool} className="tool-badge">
                  🔧 {tool}
                </span>
              ))}
            </div>
          )}
        </article>
      ))}
      <div ref={endOfMessagesRef} />
    </section>
  )
}
