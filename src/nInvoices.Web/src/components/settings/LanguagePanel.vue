<template>
  <BasePanel :title="t('settings.language.title')" :description="t('settings.language.description')">
    <BaseField :label="t('settings.language.label')" for="ui-language" :help="t('settings.language.help')">
      <select
        id="ui-language"
        class="control"
        :value="locale"
        :disabled="preferences.saving"
        @change="change(($event.target as HTMLSelectElement).value)"
      >
        <option v-for="language in availableLanguages" :key="language" :value="language">
          {{ languageName(language) }}
        </option>
      </select>
    </BaseField>
  </BasePanel>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import { availableLanguages, languageName } from '@/i18n'
import { usePreferencesStore } from '@/stores/preferences'
import { useToast } from '@/composables/useToast'

const { t, locale } = useI18n()
const preferences = usePreferencesStore()
const toast = useToast()

// The app switches at once; the choice is saved on the server, so it follows the user to every device
async function change(language: string) {
  try {
    await preferences.changeLanguage(language)
    toast.success(t('settings.language.saved'))
  } catch (error) {
    toast.failure(t('settings.language.saveFailed'), error)
  }
}
</script>

<style scoped>
.control {
  max-width: 20rem;
}
</style>
