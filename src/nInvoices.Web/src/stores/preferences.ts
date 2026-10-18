import { defineStore } from 'pinia'
import { ref } from 'vue'
import { preferencesApi } from '@/api/preferences'
import { currentLanguage, setLanguage } from '@/i18n'

/**
 * The user's preferences, stored on the server so they follow the user to every device. The app's language
 * starts as English and changes as soon as the user picks another (then saved).
 */
export const usePreferencesStore = defineStore('preferences', () => {
  const loaded = ref(false)
  const saving = ref(false)

  /** Read the user's preferences and apply them. */
  async function load() {
    const preferences = await preferencesApi.get()
    await setLanguage(preferences.uiLanguage)
    loaded.value = true
  }

  /** Switch the app to `language` now, and save it; back to the previous language if the save fails. */
  async function changeLanguage(language: string) {
    const previous = currentLanguage()
    const applied = await setLanguage(language)
    saving.value = true
    try {
      const saved = await preferencesApi.update({ uiLanguage: applied })
      await setLanguage(saved.uiLanguage)
    } catch (error) {
      await setLanguage(previous)
      throw error
    } finally {
      saving.value = false
    }
  }

  return { loaded, saving, load, changeLanguage }
})
