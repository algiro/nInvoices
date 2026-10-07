import { ref, watch } from 'vue'

export type EditorLayout = 'split' | 'code' | 'preview'

export const LAYOUT_MODES: { value: EditorLayout; label: string }[] = [
  { value: 'code', label: 'Code' },
  { value: 'split', label: 'Split' },
  { value: 'preview', label: 'Preview' }
]

const LAYOUT_KEY = 'ninvoices.templateEditor.layout'
const VARIABLES_KEY = 'ninvoices.templateEditor.variables'

function readPref(key: string, fallback: string): string {
  try {
    return localStorage.getItem(key) ?? fallback
  } catch {
    return fallback
  }
}

/** The template editor's layout and variables panel, remembered per browser. */
export function useEditorLayout() {
  const layout = ref<EditorLayout>(readPref(LAYOUT_KEY, 'split') as EditorLayout)
  // The variables panel is open by default where there is room for it
  const showVariables = ref(readPref(VARIABLES_KEY, window.innerWidth >= 1200 ? 'true' : 'false') === 'true')

  watch(layout, value => { try { localStorage.setItem(LAYOUT_KEY, value) } catch { /* not remembered */ } })
  watch(showVariables, value => { try { localStorage.setItem(VARIABLES_KEY, String(value)) } catch { /* not remembered */ } })

  return { layout, showVariables }
}
