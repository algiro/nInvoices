<template>
  <BasePanel title="Appearance" description="Pick a light and a dark theme; the mode decides which one is shown. Saved in this browser.">
    <ThemePicker />
  </BasePanel>

  <BasePanel title="Calendar" description="How the worked-days calendar lays out weeks.">
    <dl class="facts">
      <div>
        <dt>Weeks start on</dt>
        <dd>{{ firstDayOfWeekName }}</dd>
      </div>
    </dl>
    <p class="note">Set by <code>Invoice.FirstDayOfWeek</code> in the API's appsettings.json (0 = Sunday, 1 = Monday … 6 = Saturday); restart the API after changing it.</p>
  </BasePanel>

  <HolidayCalendarsPanel />
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useSettingsStore } from '@/stores/settings'
import BasePanel from '@/components/ui/BasePanel.vue'
import ThemePicker from '@/components/settings/ThemePicker.vue'
import HolidayCalendarsPanel from '@/components/settings/HolidayCalendarsPanel.vue'

const settingsStore = useSettingsStore()

const firstDayOfWeekName = computed(() => {
  const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
  const firstDay = settingsStore.invoiceSettings?.firstDayOfWeek ?? 1
  return dayNames[firstDay] || 'Monday'
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
