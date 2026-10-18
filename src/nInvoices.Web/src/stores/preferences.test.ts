// @vitest-environment jsdom
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { currentLanguage, DEFAULT_LANGUAGE, setLanguage } from '@/i18n'

vi.mock('@/api/preferences', () => ({
  preferencesApi: { get: vi.fn(), update: vi.fn() }
}))

const { preferencesApi } = await import('@/api/preferences')
const { usePreferencesStore } = await import('./preferences')

const api = vi.mocked(preferencesApi)

describe('preferences store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.resetAllMocks()
  })

  afterEach(async () => {
    await setLanguage(DEFAULT_LANGUAGE)
  })

  it('applies the language stored on the server', async () => {
    api.get.mockResolvedValue({ uiLanguage: 'it', uiLanguages: ['en', 'it'] })

    await usePreferencesStore().load()

    expect(currentLanguage()).toBe('it')
  })

  it('switches at once and saves the new language', async () => {
    api.update.mockResolvedValue({ uiLanguage: 'it', uiLanguages: ['en', 'it'] })

    await usePreferencesStore().changeLanguage('it')

    expect(api.update).toHaveBeenCalledWith({ uiLanguage: 'it' })
    expect(currentLanguage()).toBe('it')
  })

  it('goes back to the previous language when the save fails', async () => {
    api.update.mockRejectedValue(new Error('offline'))

    await expect(usePreferencesStore().changeLanguage('it')).rejects.toThrow('offline')

    expect(currentLanguage()).toBe('en')
  })
})
