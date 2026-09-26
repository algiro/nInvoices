import { ref } from 'vue'

/** A named set of list filters and sort order, as the query-string values that encode it. */
export interface SavedView {
  name: string
  query: Record<string, string>
}

/**
 * Named views of a list, kept in this browser's storage under `storageKey`. Storage can be
 * unavailable (private mode); the views then last until the page is closed.
 */
export function useSavedViews(storageKey: string) {
  const views = ref<SavedView[]>(read())

  function read(): SavedView[] {
    try {
      const parsed = JSON.parse(localStorage.getItem(storageKey) ?? '[]')
      return Array.isArray(parsed) ? parsed.filter(v => typeof v?.name === 'string' && v.query) : []
    } catch {
      return []
    }
  }

  function write() {
    try {
      localStorage.setItem(storageKey, JSON.stringify(views.value))
    } catch {
      // storage unavailable: the views stay in memory only
    }
  }

  /** Saves the view, replacing one with the same name. */
  function save(name: string, query: Record<string, string>) {
    const trimmed = name.trim()
    views.value = [...views.value.filter(v => v.name !== trimmed), { name: trimmed, query: { ...query } }]
      .sort((a, b) => a.name.localeCompare(b.name))
    write()
  }

  function remove(name: string) {
    views.value = views.value.filter(v => v.name !== name)
    write()
  }

  /** The saved view with exactly these values, if any. */
  function find(query: Record<string, string>): SavedView | undefined {
    const key = canonical(query)
    return views.value.find(v => canonical(v.query) === key)
  }

  return { views, save, remove, find }
}

function canonical(query: Record<string, string>): string {
  return JSON.stringify(Object.entries(query).filter(([, v]) => v !== '').sort(([a], [b]) => a.localeCompare(b)))
}
