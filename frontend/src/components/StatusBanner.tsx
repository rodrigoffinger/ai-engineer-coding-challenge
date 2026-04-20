import type { StatusMessage } from '../types/chat'

interface StatusBannerProps {
  status: StatusMessage
}

export function StatusBanner({ status }: StatusBannerProps) {
  return (
    <section className="status-banner" data-tone={status.tone} aria-live="polite">
      {status.message}
    </section>
  )
}