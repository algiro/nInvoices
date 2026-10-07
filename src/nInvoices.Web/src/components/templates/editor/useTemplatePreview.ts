import { ref, computed, watch, onBeforeUnmount, type Ref } from 'vue'
import { useToast } from '@/composables/useToast'
import type { EditorProblem } from './TemplateCodeEditor.vue'
import type { TemplateKindApi } from './templateKinds'

export interface TemplateError extends EditorProblem {
  /** A syntax error blocks saving; a runtime error only means the sample data can't render. */
  syntax: boolean
}

/** Places a server-reported template error at its line and column, when it has one. */
export function parseTemplateError(raw: string): TemplateError {
  // Parser: "Line 3, Column 12: message"
  let m = raw.match(/^Line (\d+), Column (\d+):\s*([\s\S]*)$/)
  if (m) return { line: +m[1], column: +m[2], message: m[3], syntax: true }
  // Runtime: "<input>(3,12) : error : message"
  m = raw.match(/^<input>\((\d+),(\d+)\)\s*:\s*error\s*:\s*([\s\S]*)$/)
  if (m) return { line: +m[1], column: +m[2], message: m[3], syntax: false }
  // Email templates: "Body: …" points into the editor, "Subject: …" into the subject field
  m = raw.match(/^(Subject|Body):\s*([\s\S]*)$/)
  if (m) {
    const inner = parseTemplateError(m[2])
    return m[1] === 'Body' ? inner : { ...inner, line: 0, column: 0, message: `Subject: ${inner.message}` }
  }
  return { line: 0, column: 0, message: raw, syntax: false }
}

/**
 * The live preview of a template being edited: rendered by the server half a second after the
 * last change, with its errors. A failed render keeps the last good preview, marked stale.
 */
export function useTemplatePreview(options: {
  api: () => TemplateKindApi
  content: Ref<string>
  /** Email templates only; empty for the other kinds. */
  subject: Ref<string>
  customerId: () => number | null
  /** While loading, edits are the template arriving, not the user typing. */
  loading: Ref<boolean>
}) {
  const toast = useToast()

  const html = ref<string | null>(null)
  const subject = ref<string | null>(null)
  const busy = ref(false)
  const stale = ref(false)
  const errors = ref<TemplateError[]>([])
  const problems = computed<EditorProblem[]>(() => errors.value.filter(e => e.line > 0))
  const hasSyntaxErrors = computed(() => errors.value.some(e => e.syntax))

  let timer: ReturnType<typeof setTimeout> | null = null
  let seq = 0

  async function refresh() {
    const current = ++seq
    if (!options.content.value.trim()) {
      html.value = null
      errors.value = []
      stale.value = false
      return
    }
    busy.value = true
    try {
      const result = await options.api().preview(options.content.value, options.subject.value, options.customerId())
      if (current !== seq) return // a newer edit is already on its way
      subject.value = result.subject
      errors.value = result.errors.map(parseTemplateError)
      if (result.html !== null) {
        html.value = result.html
        stale.value = false
      } else {
        stale.value = html.value !== null
      }
    } catch (error) {
      if (current === seq) toast.failure('Preview failed', error)
    } finally {
      if (current === seq) busy.value = false
    }
  }

  /** Renders now if an edit is still waiting, so a decision (saving) sees the latest text. */
  async function flush() {
    if (!timer) return
    clearTimeout(timer)
    timer = null
    await refresh()
  }

  watch([options.content, options.subject], () => {
    if (options.loading.value) return
    if (timer) clearTimeout(timer)
    timer = setTimeout(refresh, 500)
  })

  onBeforeUnmount(() => {
    if (timer) clearTimeout(timer)
  })

  return { html, subject, busy, stale, errors, problems, hasSyntaxErrors, refresh, flush }
}
