import { useEffect, useState } from 'react'
import { apiClient } from '../services/apiClient'
import type { ChatMessage, Citation, StatusMessage } from '../types/chat'
import { ChatComposer } from '../components/ChatComposer'
import { ChatTranscript } from '../components/ChatTranscript'
import { CitationsPanel } from '../components/CitationsPanel'
import { IngestPanel } from '../components/IngestPanel'
import { StatusBanner } from '../components/StatusBanner'

const defaultSourcePath = '../../../knowledge-base/Grocery_Store_SOP.md'

function createMessage(role: ChatMessage['role'], content: string, toolCalls?: string[]): ChatMessage {
  return {
    id: window.crypto.randomUUID(),
    role,
    content,
    timestamp: new Date().toISOString(),
    toolCalls,
  }
}

export function ChatPage() {
  const [conversationId] = useState(() => window.crypto.randomUUID())
  const [draft, setDraft] = useState('')
  const [sourcePath, setSourcePath] = useState(defaultSourcePath)
  const [isSending, setIsSending] = useState(false)
  const [isIngesting, setIsIngesting] = useState(false)
  const [citations, setCitations] = useState<Citation[]>([])
  const [status, setStatus] = useState<StatusMessage>({
    tone: 'info',
    message: 'Checking backend health...',
  })
  const [messages, setMessages] = useState<ChatMessage[]>([
    createMessage(
      'assistant',
      'Hello! I\'m the Grocery Store SOP Assistant. Ask me anything about store procedures, food safety, employee policies, store hours, and more. Click "Ingest SOP" in the sidebar first if this is your first time running the app.',
    ),
  ])

  useEffect(() => {
    let isCancelled = false

    async function loadHealth() {
      try {
        const health = await apiClient.getHealth()

        if (!isCancelled) {
          setStatus({
            tone: 'success',
            message: `${health.service} is running. ${health.notes[0] ?? ''}`.trim(),
          })
        }
      } catch (error) {
        if (!isCancelled) {
          setStatus({
            tone: 'warning',
            message: error instanceof Error
              ? `Backend health check failed: ${error.message}`
              : 'Backend health check failed.',
          })
        }
      }
    }

    void loadHealth()

    return () => {
      isCancelled = true
    }
  }, [])

  async function handleIngest() {
    setIsIngesting(true)
    setStatus({ tone: 'info', message: 'Calling the ingest endpoint...' })

    try {
      const response = await apiClient.ingest({
        sourcePath,
        forceReingest: false,
      })

      setStatus({
        tone: response.isPlaceholder ? 'warning' : 'success',
        message: `${response.message} Vector store: ${response.vectorStorePath}`,
      })
    } catch (error) {
      setStatus({
        tone: 'error',
        message: error instanceof Error ? error.message : 'Ingest request failed.',
      })
    } finally {
      setIsIngesting(false)
    }
  }

  async function handleSend() {
    const trimmedDraft = draft.trim()
    if (!trimmedDraft) {
      return
    }

    const userMessage = createMessage('user', trimmedDraft)
    const nextMessages = [...messages, userMessage]

    setMessages(nextMessages)
    setDraft('')
    setIsSending(true)
    setStatus({ tone: 'info', message: 'Sending chat request...' })

    try {
      const response = await apiClient.chat({
        conversationId,
        useTools: true,
        messages: nextMessages.map((message) => ({
          role: message.role,
          content: message.content,
          timestampUtc: message.timestamp,
        })),
      })

      setMessages((currentMessages) => [
        ...currentMessages,
        createMessage('assistant', response.assistantMessage, response.toolCalls.length > 0 ? response.toolCalls : undefined),
      ])
      setCitations(response.citations)
      setStatus({
        tone: response.isPlaceholder ? 'warning' : 'success',
        message: `Chat response received with status '${response.status}'.`,
      })
    } catch (error) {
      setMessages((currentMessages) => [
        ...currentMessages,
        createMessage('assistant', 'The chat request failed. Start the backend and try again.'),
      ])
      setStatus({
        tone: 'error',
        message: error instanceof Error ? error.message : 'Chat request failed.',
      })
    } finally {
      setIsSending(false)
    }
  }

  return (
    <main className="app-shell">
      <section className="chat-layout">
        <header className="app-header">
          <h1>Grocery Store SOP Assistant</h1>
          <p>
            A lightweight React shell for a multi-turn employee chatbot backed by a .NET 10 Web API.
            The ingest form is prefilled with the backend-ready local path for the provided SOP file.
          </p>
        </header>
        <StatusBanner status={status} />
        <ChatTranscript messages={messages} />
        <ChatComposer value={draft} onChange={setDraft} onSubmit={handleSend} isBusy={isSending} />
      </section>

      <aside className="sidebar">
        <IngestPanel
          sourcePath={sourcePath}
          onSourcePathChange={setSourcePath}
          onIngest={handleIngest}
          isBusy={isIngesting}
        />
        <CitationsPanel citations={citations} />
      </aside>
    </main>
  )
}