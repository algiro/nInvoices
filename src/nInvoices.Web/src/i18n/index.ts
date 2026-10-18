import { createI18n } from 'vue-i18n'
import en from '@/locales/en.json'

/**
 * The app's language. Every file in src/locales is a language: adding one is adding its file (and the API's,
 * see Docs/LOCALIZATION.md). English is bundled and is what a language falls back to for a missing text;
 * the others are loaded when picked.
 */
export type MessageSchema = typeof en

type Leaves<T, P extends string = ''> = {
  [K in keyof T & string]: T[K] extends string ? `${P}${K}` : Leaves<T[K], `${P}${K}.`>
}[keyof T & string]

/** A text's key, e.g. 'nav.invoices': for keys kept in data (menus, route titles) and translated where shown. */
export type MessageKey = Leaves<MessageSchema>

export const DEFAULT_LANGUAGE = 'en'

const STORAGE_KEY = 'ninvoices.language'

const loaders = import.meta.glob<{ default: MessageSchema }>(['../locales/*.json', '!../locales/en.json'])

/** The languages the app is available in, English first, e.g. ['en', 'it']. */
export const availableLanguages: readonly string[] = [
  DEFAULT_LANGUAGE,
  ...Object.keys(loaders).map(path => /([^/]+)\.json$/.exec(path)![1]).sort()
]

export const i18n = createI18n<[MessageSchema], string, false>({
  legacy: false,
  locale: DEFAULT_LANGUAGE,
  fallbackLocale: DEFAULT_LANGUAGE,
  messages: { [DEFAULT_LANGUAGE]: en } as Record<string, MessageSchema>,
  // A missing text shows the English one; say so while developing
  missingWarn: import.meta.env.DEV,
  fallbackWarn: false
})

/** Translate outside components (stores, composables, plain functions). */
export const t = i18n.global.t

/** The current language, e.g. 'it'. Reactive: what reads it re-renders when the language changes. */
export function currentLanguage(): string {
  return i18n.global.locale.value
}

/** The available language that serves `language` best: itself, else its base ('it-CH' → 'it'); null when none. */
export function matchLanguage(language: string | null | undefined): string | null {
  if (!language) return null
  const tag = language.trim().replace('_', '-').toLowerCase()
  return availableLanguages.find(l => l.toLowerCase() === tag)
    ?? availableLanguages.find(l => l.toLowerCase() === tag.split('-')[0])
    ?? null
}

/** A language's own name, e.g. 'it' → 'Italiano', for the language picker. */
export function languageName(language: string): string {
  try {
    const name = new Intl.DisplayNames([language], { type: 'language' }).of(language) ?? language
    return name.charAt(0).toLocaleUpperCase(language) + name.slice(1)
  } catch {
    return language
  }
}

/**
 * Switch the app to `language` (English when the app doesn't have it): loads its texts if needed, then
 * every screen follows. Remembered in this browser so the next visit starts in it before the server answers.
 */
export async function setLanguage(language: string | null | undefined): Promise<string> {
  const chosen = matchLanguage(language) ?? DEFAULT_LANGUAGE
  if (!i18n.global.availableLocales.includes(chosen)) {
    const loader = loaders[`../locales/${chosen}.json`]
    i18n.global.setLocaleMessage(chosen, (await loader()).default)
  }
  i18n.global.locale.value = chosen
  if (typeof document !== 'undefined') document.documentElement.lang = chosen
  try {
    localStorage.setItem(STORAGE_KEY, chosen)
  } catch {
    // Storage blocked: the server's preference still applies after sign-in
  }
  return chosen
}

/** The language this browser used last, until the user's preference is read from the server. */
export function rememberedLanguage(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}
