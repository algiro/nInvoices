<template>
  <LanguagePanel />

  <BasePanel :title="t('settings.general.appearance')" :description="t('settings.general.appearanceDescription')">
    <ThemePicker />
  </BasePanel>

  <BasePanel :title="t('settings.general.calendar')" :description="t('settings.general.calendarDescription')">
    <dl class="facts">
      <div>
        <dt>{{ t('settings.general.weeksStartOn') }}</dt>
        <dd>{{ firstDayOfWeekName }}</dd>
      </div>
    </dl>
    <i18n-t keypath="settings.general.weeksStartOnNote" tag="p" class="note">
      <template #setting><code>Invoice.FirstDayOfWeek</code></template>
    </i18n-t>
  </BasePanel>

  <HolidayCalendarsPanel />

  <VersionPanel />
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useSettingsStore } from '@/stores/settings'
import BasePanel from '@/components/ui/BasePanel.vue'
import ThemePicker from '@/components/settings/ThemePicker.vue'
import HolidayCalendarsPanel from '@/components/settings/HolidayCalendarsPanel.vue'
import LanguagePanel from '@/components/settings/LanguagePanel.vue'
import VersionPanel from '@/components/settings/VersionPanel.vue'
import { currentLanguage } from '@/i18n'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const settingsStore = useSettingsStore()

const firstDayOfWeekName = computed(() => {
  const firstDay = settingsStore.invoiceSettings?.firstDayOfWeek ?? 1
  // 1 January 2023 was a Sunday
  return new Date(2023, 0, 1 + (firstDay % 7)).toLocaleDateString(currentLanguage(), { weekday: 'long' })
})

onMounted(() => {
  settingsStore.fetchInvoiceSettings()
})
</script>

<style scoped>
code {
  padding: 0.05rem 0.3rem;
  border-radius: var(--radius-sm);
  background: var(--color-surface-sunken);
  font-size: 0.85em;
}

.note {
  margin: 0.75rem 0 0;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.facts {
  margin: 0;
}

.facts div {
  display: flex;
  gap: 1rem;
}

.facts dt {
  color: var(--color-text-muted);
}

.facts dd {
  margin: 0;
  font-weight: 600;
  color: var(--color-text);
}
</style>
