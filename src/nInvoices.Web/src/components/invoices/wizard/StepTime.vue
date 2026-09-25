<template>
  <BasePanel>
    <template #header>
      <h2 class="panel-heading">Worked days · {{ periodLabel }}</h2>
      <p class="panel-help">
        <template v-if="timeView === 'calendar'">Weekdays start as full days. Select the days that differ and edit them on the right.</template>
        <template v-else>Edit any day in its row. Click a day number to select it (Shift for a range) and split it across projects on the right.</template>
      </p>
      <p v-if="holidayCountryName" class="panel-help holidays">
        <AppIcon name="calendar" />
        <span v-if="holidays.length">
          Public holidays in {{ holidayCountryName }}:
          <template v-for="(holiday, index) in holidays" :key="holiday.date">{{ index ? ', ' : '' }}<strong>{{ dayLabel(holiday.date) }}</strong> {{ holiday.name }}</template>.
          Weekdays among them are marked as holidays.
        </span>
        <span v-else>No public holidays in {{ holidayCountryName }} this month.</span>
        <router-link to="/settings">Edit holidays</router-link>
      </p>
    </template>
    <template #actions>
      <div class="view-switch" role="group" aria-label="View">
        <button
          v-for="view in timeViews"
          :key="view.value"
          type="button"
          :class="{ active: timeView === view.value }"
          :aria-pressed="timeView === view.value"
          @click="timeView = view.value"
        >
          {{ view.label }}
        </button>
      </div>
      <BaseButton size="sm" @click="fillWeekdays">Fill weekdays</BaseButton>
      <BaseButton size="sm" variant="ghost" @click="workMonth.clearMonth">Clear</BaseButton>
    </template>

    <div class="time-layout">
      <MonthCalendar v-if="timeView === 'calendar'" :month="workMonth" />
      <TimesheetList
        v-else
        :month="workMonth"
        :rate-type="selectedRate?.type ?? null"
        :rate-amount="selectedRate?.price?.amount ?? null"
        :currency="selectedRate?.price?.currency ?? null"
        :project-suggestions="projectSuggestions"
      />
      <DayInspector
        :month="workMonth"
        :rate-type="selectedRate?.type ?? null"
        :rate-amount="selectedRate?.price?.amount ?? null"
        :currency="selectedRate?.price?.currency ?? null"
        :project-suggestions="projectSuggestions"
      />
    </div>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { InvoiceDraft } from '@/composables/useInvoiceDraft'
import MonthCalendar from '@/components/time/MonthCalendar.vue'
import DayInspector from '@/components/time/DayInspector.vue'
import TimesheetList from '@/components/time/TimesheetList.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'

const props = defineProps<{ draft: InvoiceDraft }>()

const { workMonth, selectedRate, projectSuggestions, periodLabel, holidays, holidayCountryName, fillWeekdays } = props.draft

function dayLabel(date: string) {
  return new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' })
}

// Calendar and List edit the same data; the chosen view is remembered per browser
type TimeView = 'calendar' | 'list'
const TIME_VIEW_KEY = 'ninvoices.timeView'
const timeViews: { value: TimeView; label: string }[] = [
  { value: 'calendar', label: 'Calendar' },
  { value: 'list', label: 'List' }
]
const timeView = ref<TimeView>(readTimeView())

function readTimeView(): TimeView {
  try {
    return localStorage.getItem(TIME_VIEW_KEY) === 'list' ? 'list' : 'calendar'
  } catch {
    return 'calendar'
  }
}

watch(timeView, view => {
  try {
    localStorage.setItem(TIME_VIEW_KEY, view)
  } catch {
    // storage unavailable (private mode): the choice just isn't remembered
  }
})
</script>

<style scoped>
.panel-heading {
  font-size: 1rem;
  font-weight: 600;
}

.panel-help {
  margin: 0.2rem 0 0;
  max-width: 70ch;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.holidays .app-icon {
  width: 0.9rem;
  height: 0.9rem;
  margin-right: 0.3rem;
  vertical-align: -0.12em;
  color: var(--color-primary);
}

.holidays a {
  margin-left: 0.35rem;
}

.holidays strong {
  color: var(--color-text-secondary);
  font-weight: 600;
}

.view-switch {
  display: inline-flex;
  gap: 2px;
  padding: 2px;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
}

.view-switch button {
  padding: 0.25rem 0.7rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
}

.view-switch button:hover {
  background: var(--color-surface-sunken);
  color: var(--color-text);
}

.view-switch button.active {
  background: var(--color-primary);
  color: var(--color-on-primary);
  font-weight: 600;
}

.time-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 20rem;
  gap: 1rem;
  align-items: start;
}

@media (max-width: 1000px) {
  .time-layout {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
