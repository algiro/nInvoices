import { ref, computed } from 'vue'

export type ThemePreference = 'system' | 'light' | 'dark'
export type Theme = 'light' | 'dark'

// Must match the inline script in index.html, which applies the theme before the app loads
const STORAGE_KEY = 'ninvoices.theme'

const media = typeof window !== 'undefined' ? window.matchMedia('(prefers-color-scheme: dark)') : null

function readPreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    return stored === 'light' || stored === 'dark' ? stored : 'system'
  } catch {
    return 'system'
  }
}

// Module-level so every component shares one preference
const preference = ref<ThemePreference>(readPreference())
const systemDark = ref(media?.matches ?? false)

const theme = computed<Theme>(() =>
  preference.value === 'system' ? (systemDark.value ? 'dark' : 'light') : preference.value
)

function apply() {
  document.documentElement.dataset.theme = theme.value
}

// "System" follows the OS live, e.g. when it switches to dark in the evening
media?.addEventListener('change', event => {
  systemDark.value = event.matches
  apply()
})

function setPreference(value: ThemePreference) {
  preference.value = value
  try {
    if (value === 'system') localStorage.removeItem(STORAGE_KEY)
    else localStorage.setItem(STORAGE_KEY, value)
  } catch {
    // storage unavailable (private mode): the choice lasts for this visit only
  }
  apply()
}

export function useTheme() {
  return { preference, theme, setPreference }
}

/** Called once at startup, in case the inline script in index.html didn't run. */
export function initTheme() {
  apply()
}
