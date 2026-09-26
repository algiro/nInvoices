<template>
  <BasePanel title="Public holidays" description="The holidays marked on time sheets, per country. Each customer uses the calendar of its address country, or the one chosen on the customer.">
    <template #actions>
      <BaseButton v-if="calendar?.hasBuiltIn" size="sm" variant="ghost" icon="refresh" :disabled="busy" @click="handleReset">Reset to built-in</BaseButton>
      <BaseButton size="sm" icon="plus" :disabled="!countryCode || busy" @click="openForm(null)">Add holiday</BaseButton>
    </template>

    <div class="toolbar">
      <BaseField label="Country" for="holiday-country">
        <select id="holiday-country" v-model="countryCode" class="control">
          <option v-for="country in countries" :key="country.countryCode" :value="country.countryCode">
            {{ country.countryName }} ({{ country.countryCode }})
          </option>
          <option value="__other">Another country…</option>
        </select>
      </BaseField>
      <BaseField v-if="countryCode === '__other' || otherCode" label="Country code" for="holiday-other-code" help="Two letters, e.g. CH or NL.">
        <div class="other-code">
          <input id="holiday-other-code" v-model="otherCodeInput" type="text" maxlength="2" class="control code" @keydown.enter.prevent="useOtherCode" />
          <BaseButton size="sm" @click="useOtherCode">Open</BaseButton>
        </div>
      </BaseField>
      <BaseField label="Dates in" for="holiday-year">
        <div class="year">
          <BaseButton icon="arrowLeft" icon-only size="sm" aria-label="Previous year" @click="year--" />
          <input id="holiday-year" v-model.number="year" type="number" class="control num" min="1900" max="2200" />
          <BaseButton icon="chevronRight" icon-only size="sm" aria-label="Next year" @click="year++" />
        </div>
      </BaseField>
    </div>

    <LoadingState v-if="loading" label="Loading holidays…" />

    <EmptyState
      v-else-if="activeCode && (!calendar || calendar.rules.length === 0)"
      compact
      icon="calendar"
      :title="`No holidays for ${activeCode}`"
      description="There are no built-in holidays for this country. Add them one by one; they're used for every customer in this country."
    />

    <div v-else-if="calendar" class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">Holiday</th>
            <th scope="col">Rule</th>
            <th scope="col">{{ year }}</th>
            <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="rule in calendar.rules" :key="rule.id" :class="{ inactive: !rule.isActive }">
            <td class="primary-cell">
              {{ rule.name }}
              <StatusPill v-if="!rule.isActive" tone="neutral">Off</StatusPill>
            </td>
            <td>
              {{ describeRule(rule) }}
              <span v-if="rule.fromYear || rule.toYear" class="muted years">{{ describeYears(rule) }}</span>
            </td>
            <td class="date-cell">{{ dateIn(rule) }}</td>
            <td class="actions">
              <BaseButton size="sm" variant="ghost" icon="edit" @click="openForm(rule)">Edit</BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                title="Delete"
                :aria-label="`Delete ${rule.name}`"
                @click="handleDelete(rule)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <BaseDialog :open="formOpen" :title="editing ? 'Edit holiday' : 'New holiday'" size="sm" :close-on-overlay="false" @close="formOpen = false">
      <form id="holiday-form" class="rule-form" novalidate @submit.prevent="handleSave">
        <BaseField label="Name" for="rule-name" required :error="formError">
          <input id="rule-name" v-model="draft.name" type="text" class="control" placeholder="e.g. Sant'Ambrogio" />
        </BaseField>

        <BaseField label="Date" for="rule-kind">
          <select id="rule-kind" v-model="draft.kind" class="control">
            <option :value="HolidayRuleKind.Fixed">Same date every year</option>
            <option :value="HolidayRuleKind.EasterOffset">Days from Easter Sunday</option>
            <option :value="HolidayRuleKind.NthWeekday">A weekday of a month</option>
          </select>
        </BaseField>

        <div v-if="draft.kind === HolidayRuleKind.Fixed" class="row">
          <BaseField label="Day" for="rule-day">
            <input id="rule-day" v-model.number="draft.day" type="number" min="1" max="31" class="control num" />
          </BaseField>
          <BaseField label="Month" for="rule-month">
            <select id="rule-month" v-model.number="draft.month" class="control">
              <option v-for="m in monthOptions" :key="m.value" :value="m.value">{{ m.label }}</option>
            </select>
          </BaseField>
        </div>

        <BaseField
          v-else-if="draft.kind === HolidayRuleKind.EasterOffset"
          label="Days from Easter Sunday"
          for="rule-offset"
          help="1 = Easter Monday, -2 = Good Friday, 39 = Ascension, 50 = Whit Monday, 60 = Corpus Christi."
        >
          <input id="rule-offset" v-model.number="draft.easterOffset" type="number" min="-100" max="100" class="control num" />
        </BaseField>

        <div v-else class="row three">
          <BaseField label="Which" for="rule-occurrence">
            <select id="rule-occurrence" v-model.number="draft.occurrence" class="control">
              <option v-for="o in occurrenceOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
            </select>
          </BaseField>
          <BaseField label="Weekday" for="rule-weekday">
            <select id="rule-weekday" v-model="draft.weekday" class="control">
              <option v-for="w in weekdays" :key="w" :value="w">{{ w }}</option>
            </select>
          </BaseField>
          <BaseField label="Of" for="rule-nth-month">
            <select id="rule-nth-month" v-model.number="draft.month" class="control">
              <option v-for="m in monthOptions" :key="m.value" :value="m.value">{{ m.label }}</option>
            </select>
          </BaseField>
        </div>

        <div class="row">
          <BaseField label="From year" for="rule-from" optional>
            <input id="rule-from" v-model.number="fromYearValue" type="number" class="control num" placeholder="Always" />
          </BaseField>
          <BaseField label="Until year" for="rule-to" optional>
            <input id="rule-to" v-model.number="toYearValue" type="number" class="control num" placeholder="Still applies" />
          </BaseField>
        </div>

        <label class="check">
          <input v-model="draft.isActive" type="checkbox" />
          Mark this day on time sheets
        </label>
      </form>

      <template #footer>
        <BaseButton :disabled="saving" @click="formOpen = false">Cancel</BaseButton>
        <BaseButton type="submit" form="holiday-form" variant="primary" :loading="saving">{{ editing ? 'Save' : 'Add holiday' }}</BaseButton>
      </template>
    </BaseDialog>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { holidaysApi } from '@/api/holidays'
import { HolidayRuleKind } from '@/types'
import type { HolidayCalendarDto, HolidayCountryDto, HolidayRuleDto, SaveHolidayRuleDto, Weekday } from '@/types'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import StatusPill from '@/components/ui/StatusPill.vue'

const toast = useToast()
const { confirm } = useConfirm()

const countries = ref<HolidayCountryDto[]>([])
const countryCode = ref('')
// A country without built-in holidays, typed by the user
const otherCode = ref('')
const otherCodeInput = ref('')
const year = ref(new Date().getFullYear())
const calendar = ref<HolidayCalendarDto | null>(null)
const loading = ref(false)
const busy = ref(false)

const activeCode = computed(() => (countryCode.value === '__other' ? otherCode.value : countryCode.value))

const weekdays: Weekday[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']
const monthOptions = Array.from({ length: 12 }, (_, i) => ({
  value: i + 1,
  label: new Date(2000, i, 1).toLocaleDateString('en-US', { month: 'long' })
}))
const occurrenceOptions = [
  { value: 1, label: 'First' },
  { value: 2, label: 'Second' },
  { value: 3, label: 'Third' },
  { value: 4, label: 'Fourth' },
  { value: 5, label: 'Fifth' },
  { value: -1, label: 'Last' }
]

onMounted(async () => {
  try {
    countries.value = await holidaysApi.getCountries()
    // Start from the country most used here: Italy when available, else the first one
    countryCode.value = countries.value.find(c => c.countryCode === 'IT')?.countryCode ?? countries.value[0]?.countryCode ?? ''
  } catch (error) {
    toast.failure('Could not load the holiday countries', error)
  }
})

watch([activeCode, year], load)

async function load() {
  calendar.value = null
  const code = activeCode.value
  if (!code || !(year.value >= 1900 && year.value <= 2200)) return
  loading.value = true
  try {
    calendar.value = await holidaysApi.getCalendar(code, year.value)
  } catch (error: any) {
    // 404: a country with no built-in holidays and none added yet
    if (error?.response?.status !== 404) toast.failure('Could not load the holidays', error)
  } finally {
    loading.value = false
  }
}

function useOtherCode() {
  const code = otherCodeInput.value.trim().toUpperCase()
  if (!/^[A-Z]{2}$/.test(code)) {
    toast.warning('Enter a two-letter country code', { message: 'For example CH for Switzerland or NL for the Netherlands.' })
    return
  }
  const known = countries.value.find(c => c.countryCode === code)
  if (known) {
    countryCode.value = code
    otherCode.value = ''
  } else {
    countryCode.value = '__other'
    otherCode.value = code
  }
}

// ---------- descriptions ----------

function monthName(month: number | null) {
  return month ? monthOptions[month - 1].label : ''
}

function describeRule(rule: HolidayRuleDto): string {
  switch (rule.kind) {
    case HolidayRuleKind.Fixed:
      return `${rule.day} ${monthName(rule.month)}`
    case HolidayRuleKind.EasterOffset: {
      const n = rule.easterOffset ?? 0
      if (n === 0) return 'Easter Sunday'
      return `${Math.abs(n)} day${Math.abs(n) === 1 ? '' : 's'} ${n > 0 ? 'after' : 'before'} Easter Sunday`
    }
    case HolidayRuleKind.NthWeekday: {
      const which = occurrenceOptions.find(o => o.value === rule.occurrence)?.label ?? ''
      return `${which} ${rule.weekday} of ${monthName(rule.month)}`
    }
  }
}

function describeYears(rule: HolidayRuleDto): string {
  if (rule.fromYear && rule.toYear) return `${rule.fromYear}–${rule.toYear}`
  return rule.fromYear ? `from ${rule.fromYear}` : `until ${rule.toYear}`
}

/** The rule's date in the chosen year, from the holidays the server computed. */
function dateIn(rule: HolidayRuleDto): string {
  if (!rule.isActive) return '–'
  const holiday = calendar.value?.holidays.find(h => h.name === rule.name)
  if (!holiday) return 'Not this year'
  return new Date(`${holiday.date}T00:00:00`).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' })
}

// ---------- edit ----------

const formOpen = ref(false)
const saving = ref(false)
const editing = ref<HolidayRuleDto | null>(null)
const formError = ref<string | null>(null)
const draft = reactive<SaveHolidayRuleDto>(emptyDraft())

function emptyDraft(): SaveHolidayRuleDto {
  return {
    name: '',
    kind: HolidayRuleKind.Fixed,
    month: 1,
    day: 1,
    easterOffset: 1,
    weekday: 'Monday',
    occurrence: 1,
    fromYear: null,
    toYear: null,
    isActive: true
  }
}

// Empty year inputs mean "no limit"
const fromYearValue = computed({
  get: () => draft.fromYear ?? '',
  set: (value: number | string) => { draft.fromYear = value === '' || value === null ? null : Number(value) }
})
const toYearValue = computed({
  get: () => draft.toYear ?? '',
  set: (value: number | string) => { draft.toYear = value === '' || value === null ? null : Number(value) }
})

function openForm(rule: HolidayRuleDto | null) {
  editing.value = rule
  formError.value = null
  // Fields of the other kinds keep sensible defaults, so switching kind shows usable values
  Object.assign(draft, emptyDraft(), rule ? setFields(rule) : {})
  formOpen.value = true
  requestAnimationFrame(() => document.getElementById('rule-name')?.focus())
}

/** The rule's fields that have a value (the null year limits equal the defaults anyway). */
function setFields(rule: HolidayRuleDto): Partial<SaveHolidayRuleDto> {
  const { id: _id, ...fields } = rule
  return Object.fromEntries(Object.entries(fields).filter(([, value]) => value !== null))
}

/** Only the fields the chosen kind uses are sent. */
function payload(): SaveHolidayRuleDto {
  const base = { name: draft.name.trim(), kind: draft.kind, fromYear: draft.fromYear, toYear: draft.toYear, isActive: draft.isActive }
  const none = { month: null, day: null, easterOffset: null, weekday: null, occurrence: null }
  switch (draft.kind) {
    case HolidayRuleKind.Fixed:
      return { ...base, ...none, month: draft.month, day: draft.day }
    case HolidayRuleKind.EasterOffset:
      return { ...base, ...none, easterOffset: draft.easterOffset }
    default:
      return { ...base, ...none, month: draft.month, weekday: draft.weekday, occurrence: draft.occurrence }
  }
}

async function handleSave() {
  formError.value = draft.name.trim() ? null : 'Enter the holiday’s name.'
  if (formError.value) return

  saving.value = true
  try {
    if (editing.value) {
      await holidaysApi.updateRule(editing.value.id, payload())
    } else {
      await holidaysApi.createRule(activeCode.value, payload())
      if (!countries.value.some(c => c.countryCode === activeCode.value)) {
        countries.value = await holidaysApi.getCountries()
      }
    }
    formOpen.value = false
    toast.success(editing.value ? 'Holiday saved' : 'Holiday added')
    await load()
  } catch (error) {
    toast.failure('Could not save the holiday', error)
  } finally {
    saving.value = false
  }
}

async function handleDelete(rule: HolidayRuleDto) {
  const confirmed = await confirm({
    title: `Delete ${rule.name}?`,
    message: 'It won’t be marked on time sheets any more. To skip it only for a while, edit it and turn it off instead.',
    confirmLabel: 'Delete',
    tone: 'danger'
  })
  if (!confirmed) return

  busy.value = true
  try {
    await holidaysApi.deleteRule(rule.id)
    await load()
  } catch (error) {
    toast.failure('Could not delete the holiday', error)
  } finally {
    busy.value = false
  }
}

async function handleReset() {
  const confirmed = await confirm({
    title: `Reset ${calendar.value?.countryName} to the built-in holidays?`,
    message: 'Your changes to this country’s holidays are replaced by the built-in list.',
    confirmLabel: 'Reset',
    tone: 'danger'
  })
  if (!confirmed) return

  busy.value = true
  try {
    calendar.value = await holidaysApi.reset(activeCode.value, year.value)
    toast.success('Holidays reset')
  } catch (error) {
    toast.failure('Could not reset the holidays', error)
  } finally {
    busy.value = false
  }
}
</script>

<style scoped>
.toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 1rem;
  margin-bottom: 1rem;
}

.toolbar > * {
  min-width: 12rem;
}

.other-code,
.year {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.code {
  width: 4.5rem;
  text-transform: uppercase;
}

.year .control {
  width: 5.5rem;
}

.years {
  margin-left: 0.4rem;
  font-size: var(--text-sm);
}

.date-cell {
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
}

tr.inactive td {
  color: var(--color-text-muted);
}

.rule-form {
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
}

.row {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
}

.row.three {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.check {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: var(--text-md);
}
</style>
