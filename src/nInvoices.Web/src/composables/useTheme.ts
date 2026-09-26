import { ref, computed } from 'vue'

/** Which of the two chosen themes is shown: the light one, the dark one, or the one the OS asks for. */
export type ThemePreference = 'system' | 'light' | 'dark'
export type ThemeMode = 'light' | 'dark'

export interface ThemeInfo {
  /** Value of <html data-theme>; must match a block in themes.css */
  id: string
  name: string
  mode: ThemeMode
  description: string
}

export const THEMES: ThemeInfo[] = [
  { id: 'light', name: 'Classic', mode: 'light', description: 'Calm blue on white, no frills.' },
  { id: 'lagoon', name: 'Lagoon', mode: 'light', description: 'Turquoise sidebar on an airy canvas, colourful figures.' },
  { id: 'iris', name: 'Iris', mode: 'light', description: 'Deep indigo sidebar, violet and fuchsia accents.' },
  { id: 'ember', name: 'Ember', mode: 'light', description: 'Rust sidebar on warm cream, orange and amber accents.' },
  { id: 'dark', name: 'Classic', mode: 'dark', description: 'Muted blue-grey, easy on the eyes.' },
  { id: 'aurora', name: 'Aurora', mode: 'dark', description: 'Near-black glass panels with a teal glow.' },
  { id: 'volt', name: 'Volt', mode: 'dark', description: 'Black and graphite with electric lime.' },
  { id: 'orchid', name: 'Orchid', mode: 'dark', description: 'Warm charcoal with lavender and peach.' },
  { id: 'cinder', name: 'Cinder', mode: 'dark', description: 'Warm near-black with glowing orange.' }
]

// Must match the inline script in index.html, which applies the theme before the app loads
const MODE_KEY = 'ninvoices.theme'
const THEME_KEYS: Record<ThemeMode, string> = { light: 'ninvoices.theme.light', dark: 'ninvoices.theme.dark' }
const DEFAULTS: Record<ThemeMode, string> = { light: 'light', dark: 'dark' }

const media = typeof window !== 'undefined' ? window.matchMedia('(prefers-color-scheme: dark)') : null

function read(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function write(key: string, value: string | null) {
  try {
    if (value === null) localStorage.removeItem(key)
    else localStorage.setItem(key, value)
  } catch {
    // storage unavailable (private mode): the choice lasts for this visit only
  }
}

function readPreference(): ThemePreference {
  const stored = read(MODE_KEY)
  return stored === 'light' || stored === 'dark' ? stored : 'system'
}

function readTheme(mode: ThemeMode): string {
  const stored = read(THEME_KEYS[mode])
  return THEMES.some(t => t.id === stored && t.mode === mode) ? stored! : DEFAULTS[mode]
}

// Module-level so every component shares one state
const preference = ref<ThemePreference>(readPreference())
const lightTheme = ref(readTheme('light'))
const darkTheme = ref(readTheme('dark'))
const systemDark = ref(media?.matches ?? false)

/** The mode in effect: the chosen one, or the OS's for "system". */
const mode = computed<ThemeMode>(() =>
  preference.value === 'system' ? (systemDark.value ? 'dark' : 'light') : preference.value
)

/** The theme in effect (an id from THEMES). */
const theme = computed(() => (mode.value === 'dark' ? darkTheme.value : lightTheme.value))

function apply() {
  document.documentElement.dataset.theme = theme.value
  document.documentElement.dataset.mode = mode.value
}

// "System" follows the OS live, e.g. when it switches to dark in the evening
media?.addEventListener('change', event => {
  systemDark.value = event.matches
  apply()
})

function setPreference(value: ThemePreference) {
  preference.value = value
  write(MODE_KEY, value === 'system' ? null : value)
  apply()
}

/** Chooses the light or dark theme (by the theme's own mode) and shows it right away. */
function setTheme(id: string) {
  const info = THEMES.find(t => t.id === id)
  if (!info) return
  if (info.mode === 'dark') darkTheme.value = id
  else lightTheme.value = id
  write(THEME_KEYS[info.mode], id === DEFAULTS[info.mode] ? null : id)
  // Picking a theme of the other mode switches to it, so the choice is visible at once
  if (info.mode !== mode.value) setPreference(info.mode)
  else apply()
}

export function useTheme() {
  return { preference, mode, theme, lightTheme, darkTheme, setPreference, setTheme }
}

/** Called once at startup, in case the inline script in index.html didn't run. */
export function initTheme() {
  apply()
}
