<template>
  <BasePanel :title="t('customers.form.publicHolidays')" :description="t('holidays.description')">
    <template #actions>
      <BaseButton v-if="calendar?.hasBuiltIn" size="sm" variant="ghost" icon="refresh" :disabled="busy" @click="handleReset">{{ t('holidays.resetBuiltIn') }}</BaseButton>
      <BaseButton size="sm" icon="plus" :disabled="!countryCode || busy" @click="openForm(null)">{{ t('holidays.add') }}</BaseButton>
    </template>

    <div class="toolbar">
      <BaseField :label="t('address.country')" for="holiday-country">
        <select id="holiday-country" v-model="countryCode" class="control">
          <option v-for="country in countries" :key="country.countryCode" :value="country.countryCode">
            {{ regionName(country.countryCode, country.countryName) }} ({{ country.countryCode }})
          </option>
          <option value="__other">{{ t('holidays.anotherCountry') }}</option>
        </select>
      </BaseField>
      <BaseField v-if="countryCode === '__other' || otherCode" :label="t('holidays.countryCode')" for="holiday-other-code" :help="t('holidays.countryCodeHelp')">
        <div class="other-code">
          <input id="holiday-other-code" v-model="otherCodeInput" type="text" maxlength="2" class="control code" @keydown.enter.prevent="useOtherCode" />
          <BaseButton size="sm" @click="useOtherCode">{{ t('dashboard.open') }}</BaseButton>
        </div>
      </BaseField>
      <BaseField :label="t('holidays.datesIn')" for="holiday-year">
        <div class="year">
          <BaseButton icon="arrowLeft" icon-only size="sm" :aria-label="t('holidays.previousYear')" @click="year--" />
          <input id="holiday-year" v-model.number="year" type="number" class="control num" min="1900" max="2200" />
          <BaseButton icon="chevronRight" icon-only size="sm" :aria-label="t('holidays.nextYear')" @click="year++" />
        </div>
      </BaseField>
    </div>

    <LoadingState v-if="loading" :label="t('holidays.loading')" />

    <EmptyState
      v-else-if="activeCode && (!calendar || calendar.rules.length === 0)"
      compact
      icon="calendar"
      :title="t('holidays.noneFor', { country: activeCode })"
      :description="t('holidays.noneDescription')"
    />

    <div v-else-if="calendar" class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">{{ t('holidays.holiday') }}</th>
            <th scope="col">{{ t('holidays.rule') }}</th>
            <th scope="col">{{ year }}</th>
            <th scope="col" class="actions"><span class="sr-only">{{ t('common.actions') }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="rule in calendar.rules" :key="rule.id" :class="{ inactive: !rule.isActive }">
            <td class="primary-cell">
              {{ rule.name }}
              <StatusPill v-if="!rule.isActive" tone="neutral">{{ t('holidays.off') }}</StatusPill>
            </td>
            <td>
              {{ describeRule(rule) }}
              <span v-if="rule.fromYear || rule.toYear" class="muted years">{{ describeYears(rule) }}</span>
            </td>
            <td class="date-cell">{{ dateIn(rule) }}</td>
            <td class="actions">
              <BaseButton size="sm" variant="ghost" icon="edit" @click="openForm(rule)">{{ t('common.edit') }}</BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                :title="t('common.delete')"
                :aria-label="t('common.deleteNamed', { name: rule.name })"
                @click="handleDelete(rule)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <BaseDialog :open="formOpen" :title="editing ? t('holidays.edit') : t('holidays.new')" size="sm" :close-on-overlay="false" @close="formOpen = false">
      <form id="holiday-form" class="rule-form" novalidate @submit.prevent="handleSave">
        <BaseField :label="t('customers.name')" for="rule-name" required :error="formError">
          <input id="rule-name" v-model="draft.name" type="text" class="control" :placeholder="t('holidays.namePlaceholder')" />
        </BaseField>

        <BaseField :label="t('expenses.date')" for="rule-kind">
          <select id="rule-kind" v-model="draft.kind" class="control">
            <option :value="HolidayRuleKind.Fixed">{{ t('holidays.kinds.fixed') }}</option>
            <option :value="HolidayRuleKind.EasterOffset">{{ t('holidays.kinds.easter') }}</option>
            <option :value="HolidayRuleKind.NthWeekday">{{ t('holidays.kinds.nthWeekday') }}</option>
          </select>
        </BaseField>

        <div v-if="draft.kind === HolidayRuleKind.Fixed" class="row">
          <BaseField :label="t('time.day')" for="rule-day">
            <input id="rule-day" v-model.number="draft.day" type="number" min="1" max="31" class="control num" />
          </BaseField>
          <BaseField :label="t('common.month')" for="rule-month">
            <select id="rule-month" v-model.number="draft.month" class="control">
              <option v-for="m in monthOptions" :key="m.value" :value="m.value">{{ m.label }}</option>
            </select>
          </BaseField>
        </div>

        <BaseField
          v-else-if="draft.kind === HolidayRuleKind.EasterOffset"
          :label="t('holidays.kinds.easter')"
          for="rule-offset"
          :help="t('holidays.easterHelp')"
        >
          <input id="rule-offset" v-model.number="draft.easterOffset" type="number" min="-100" max="100" class="control num" />
        </BaseField>

        <div v-else class="row three">
          <BaseField :label="t('holidays.which')" for="rule-occurrence">
            <select id="rule-occurrence" v-model.number="draft.occurrence" class="control">
              <option v-for="o in occurrenceOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
            </select>
          </BaseField>
          <BaseField :label="t('time.weekday')" for="rule-weekday">
            <select id="rule-weekday" v-model="draft.weekday" class="control">
              <option v-for="w in weekdays" :key="w" :value="w">{{ weekdayName(w) }}</option>
            </select>
          </BaseField>
          <BaseField :label="t('holidays.of')" for="rule-nth-month">
            <select id="rule-nth-month" v-model.number="draft.month" class="control">
              <option v-for="m in monthOptions" :key="m.value" :value="m.value">{{ m.label }}</option>
            </select>
          </BaseField>
        </div>

        <div class="row">
          <BaseField :label="t('holidays.fromYear')" for="rule-from" optional>
            <input id="rule-from" v-model.number="fromYearValue" type="number" class="control num" :placeholder="t('holidays.always')" />
          </BaseField>
          <BaseField :label="t('holidays.untilYear')" for="rule-to" optional>
            <input id="rule-to" v-model.number="toYearValue" type="number" class="control num" :placeholder="t('holidays.stillApplies')" />
          </BaseField>
        </div>

        <label class="check">
          <input v-model="draft.isActive" type="checkbox" />
          {{ t('holidays.markOnTimesheets') }}
        </label>
      </form>

      <template #footer>
        <BaseButton :disabled="saving" @click="formOpen = false">{{ t('common.cancel') }}</BaseButton>
        <BaseButton type="submit" form="holiday-form" variant="primary" :loading="saving">{{ editing ? t('common.save') : t('holidays.add') }}</BaseButton>
      </template>
    </BaseDialog>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { holidaysApi } from '@/api/holidays'
import { HolidayRuleKind } from '@/types'
import type { HolidayCalendarDto, HolidayCountryDto, HolidayRuleDto, SaveHolidayRuleDto, Weekday } from '@/types'
import { regionName } from '@/utils/format'
import { useToast, apiStatus } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import { currentLanguage } from '@/i18n'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

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
/** 'Monday' → its name in the app's language (2 January 2023 was a Monday) */
function weekdayName(weekday: Weekday | string | null): string {
  const index = weekdays.indexOf(weekday as Weekday)
  return index < 0 ? String(weekday ?? '') : new Date(2023, 0, 2 + index).toLocaleDateString(currentLanguage(), { weekday: 'long' })
}
const monthOptions = computed(() => Array.from({ length: 12 }, (_, i) => ({
  value: i + 1,
  label: new Date(2000, i, 1).toLocaleDateString(currentLanguage(), { month: 'long' })
})))
const occurrenceOptions = computed(() => [
  { value: 1, label: t('holidays.occurrence.first') },
  { value: 2, label: t('holidays.occurrence.second') },
  { value: 3, label: t('holidays.occurrence.third') },
  { value: 4, label: t('holidays.occurrence.fourth') },
  { value: 5, label: t('holidays.occurrence.fifth') },
  { value: -1, label: t('holidays.occurrence.last') }
])

onMounted(async () => {
  try {
    countries.value = await holidaysApi.getCountries()
    // Start from the country most used here: Italy when available, else the first one
    countryCode.value = countries.value.find(c => c.countryCode === 'IT')?.countryCode ?? countries.value[0]?.countryCode ?? ''
  } catch (error) {
    toast.failure(t('holidays.countriesFailed'), error)
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
  } catch (error) {
    // 404: a country with no built-in holidays and none added yet
    if (apiStatus(error) !== 404) toast.failure(t('holidays.loadFailed'), error)
  } finally {
    loading.value = false
  }
}

function useOtherCode() {
  const code = otherCodeInput.value.trim().toUpperCase()
  if (!/^[A-Z]{2}$/.test(code)) {
    toast.warning(t('holidays.enterCode'), { message: t('holidays.enterCodeHelp') })
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
  return month ? monthOptions.value[month - 1].label : ''
}

function describeRule(rule: HolidayRuleDto): string {
  switch (rule.kind) {
    case HolidayRuleKind.Fixed:
      return `${rule.day} ${monthName(rule.month)}`
    case HolidayRuleKind.EasterOffset: {
      const n = rule.easterOffset ?? 0
      if (n === 0) return t('holidays.easterSunday')
      return n > 0 ? t('holidays.daysAfterEaster', Math.abs(n)) : t('holidays.daysBeforeEaster', Math.abs(n))
    }
    case HolidayRuleKind.NthWeekday: {
      const which = occurrenceOptions.value.find(o => o.value === rule.occurrence)?.label ?? ''
      return t('holidays.nthWeekday', { which, weekday: weekdayName(rule.weekday), month: monthName(rule.month) })
    }
  }
}

function describeYears(rule: HolidayRuleDto): string {
  if (rule.fromYear && rule.toYear) return `${rule.fromYear}–${rule.toYear}`
  return rule.fromYear ? t('holidays.fromYearValue', { year: rule.fromYear }) : t('holidays.untilYearValue', { year: rule.toYear ?? '' })
}

/** The rule's date in the chosen year, from the holidays the server computed. */
function dateIn(rule: HolidayRuleDto): string {
  if (!rule.isActive) return '–'
  const holiday = calendar.value?.holidays.find(h => h.name === rule.name)
  if (!holiday) return t('holidays.notThisYear')
  return new Date(`${holiday.date}T00:00:00`).toLocaleDateString(currentLanguage(), { weekday: 'short', day: 'numeric', month: 'short' })
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
  formError.value = draft.name.trim() ? null : t('holidays.enterName')
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
    toast.success(editing.value ? t('holidays.saved') : t('holidays.added'))
    await load()
  } catch (error) {
    toast.failure(t('holidays.saveFailed'), error)
  } finally {
    saving.value = false
  }
}

async function handleDelete(rule: HolidayRuleDto) {
  const confirmed = await confirm({
    title: t('holidays.deleteTitle', { name: rule.name }),
    message: t('holidays.deleteMessage'),
    confirmLabel: t('common.delete'),
    tone: 'danger'
  })
  if (!confirmed) return

  busy.value = true
  try {
    await holidaysApi.deleteRule(rule.id)
    await load()
  } catch (error) {
    toast.failure(t('holidays.deleteFailed'), error)
  } finally {
    busy.value = false
  }
}

async function handleReset() {
  const confirmed = await confirm({
    title: t('holidays.resetTitle', { country: regionName(calendar.value?.countryCode, calendar.value?.countryName) }),
    message: t('holidays.resetMessage'),
    confirmLabel: t('holidays.reset'),
    tone: 'danger'
  })
  if (!confirmed) return

  busy.value = true
  try {
    calendar.value = await holidaysApi.reset(activeCode.value, year.value)
    toast.success(t('holidays.resetDone'))
  } catch (error) {
    toast.failure(t('holidays.resetFailed'), error)
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
