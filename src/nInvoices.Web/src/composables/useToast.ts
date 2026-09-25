import { reactive } from 'vue'

export type ToastTone = 'success' | 'error' | 'warning' | 'info'

export interface Toast {
  id: number
  tone: ToastTone
  title: string
  message?: string
}

interface ToastOptions {
  message?: string
  /** Milliseconds before auto-dismiss; 0 keeps it until closed. */
  duration?: number
}

const DEFAULT_DURATION: Record<ToastTone, number> = {
  success: 4000,
  info: 5000,
  warning: 7000,
  error: 9000
}

// Module-level so stores and plain functions can raise toasts, not only components
const toasts = reactive<Toast[]>([])
let nextId = 1

function dismiss(id: number) {
  const index = toasts.findIndex(t => t.id === id)
  if (index >= 0) toasts.splice(index, 1)
}

function show(tone: ToastTone, title: string, options: ToastOptions = {}) {
  const id = nextId++
  toasts.push({ id, tone, title, message: options.message })
  const duration = options.duration ?? DEFAULT_DURATION[tone]
  if (duration > 0) setTimeout(() => dismiss(id), duration)
  return id
}

/** Body of a failed API call ({ error, code }), when the thrown value is an HTTP error. */
function apiErrorBody(error: unknown): { error?: unknown; code?: unknown } | null {
  const data = (error as { response?: { data?: unknown } } | null)?.response?.data
  return data && typeof data === 'object' ? data as { error?: unknown; code?: unknown } : null
}

/** Machine-readable reason sent by the API with some errors, e.g. 'gmail_not_connected'. */
export function apiErrorCode(error: unknown): string | null {
  const code = apiErrorBody(error)?.code
  return typeof code === 'string' ? code : null
}

/** Turns an unknown thrown value into a readable message, preferring the API's own explanation. */
export function errorMessage(error: unknown): string {
  const apiMessage = apiErrorBody(error)?.error
  if (typeof apiMessage === 'string' && apiMessage) return apiMessage
  if (error instanceof Error && error.message) return error.message
  if (typeof error === 'string') return error
  return 'Something went wrong. Please try again.'
}

export function useToast() {
  return {
    toasts,
    dismiss,
    success: (title: string, options?: ToastOptions) => show('success', title, options),
    info: (title: string, options?: ToastOptions) => show('info', title, options),
    warning: (title: string, options?: ToastOptions) => show('warning', title, options),
    error: (title: string, options?: ToastOptions) => show('error', title, options),
    /** Error toast whose message is taken from a caught exception. */
    failure: (title: string, error: unknown) => show('error', title, { message: errorMessage(error) })
  }
}
