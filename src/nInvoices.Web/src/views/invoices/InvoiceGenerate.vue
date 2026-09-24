<template>
  <div class="invoice-generate">
    <PageHeader title="New invoice" subtitle="Choose the customer and period, check the worked days, then generate the invoice and its timesheet." />

    <form class="generate-form" novalidate @submit.prevent="handleSubmit">
      <BasePanel title="Customer &amp; period">
        <div class="grid">
          <BaseField label="Customer" for="customer" required class="span-2">
            <select id="customer" v-model="form.customerId" class="control">
              <option :value="0" disabled>Select a customer</option>
              <option v-for="customer in sortedCustomers" :key="customer.id" :value="customer.id">
                {{ customer.name }}
              </option>
            </select>
          </BaseField>

          <BaseField label="Invoice type" for="type" required>
            <select id="type" v-model="form.invoiceType" class="control">
              <option :value="InvoiceType.Monthly">Monthly</option>
              <option :value="InvoiceType.OneTime">One-time</option>
            </select>
          </BaseField>

          <BaseField label="Issue date" for="issueDate" required>
            <input id="issueDate" v-model="form.issueDate" type="date" class="control" />
          </BaseField>

          <BaseField v-if="form.invoiceType === InvoiceType.Monthly" label="Billing period" for="period-month" required class="span-2">
            <div class="period">
              <BaseButton icon="arrowLeft" icon-only variant="secondary" aria-label="Previous month" title="Previous month" @click="shiftMonth(-1)" />
              <select id="period-month" v-model="selectedMonth" class="control" aria-label="Month">
                <option v-for="month in months" :key="month.value" :value="month.value">{{ month.label }}</option>
              </select>
              <select id="period-year" v-model="selectedYear" class="control year" aria-label="Year">
                <option v-for="year in years" :key="year" :value="year">{{ year }}</option>
              </select>
              <BaseButton icon="chevronRight" icon-only variant="secondary" aria-label="Next month" title="Next month" @click="shiftMonth(1)" />
            </div>
          </BaseField>

          <BaseField
            v-if="form.invoiceType === InvoiceType.Monthly && availableTemplates.length > 0"
            label="Timesheet template"
            for="monthlyReportTemplate"
            class="span-2"
            help="Layout of the monthly report PDF."
          >
            <select id="monthlyReportTemplate" v-model="form.monthlyReportTemplateId" class="control">
              <option :value="undefined">Active template{{ activeTemplateName ? ` (${activeTemplateName})` : '' }}</option>
              <option v-for="template in availableTemplates" :key="template.id" :value="template.id">
                {{ template.name }}{{ template.isActive ? ' (active)' : '' }}
              </option>
            </select>
          </BaseField>
        </div>

        <div v-if="form.customerId" class="rate-line" :class="{ missing: noRate }">
          <template v-if="selectedRate">
            <AppIcon name="coins" />
            <span>Billed at <strong>{{ formatMoney(selectedRate.price.amount, selectedRate.price.currency) }}</strong> {{ rateUnit }}</span>
          </template>
          <template v-else-if="noRate">
            <AppIcon name="alert" />
            <span>This customer has no rate yet, so nothing can be billed.</span>
            <router-link :to="{ path: `/customers/${form.customerId}`, query: { tab: 'rates' } }">Add a rate</router-link>
          </template>
        </div>
      </BasePanel>

      <BasePanel v-if="form.invoiceType === InvoiceType.Monthly" title="Worked days">
        <template #header>
          <h2 class="panel-heading">Worked days · {{ periodLabel }}</h2>
          <p class="panel-help">
            <template v-if="timeView === 'calendar'">Weekdays start as full days. Select the days that differ and edit them on the right.</template>
            <template v-else>Edit any day in its row. Click a day number to select it (Shift for a range) and split it across projects on the right.</template>
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

      <BasePanel title="Expenses" description="Costs passed on to the customer, added after the worked time.">
        <template #actions>
          <BaseButton size="sm" icon="plus" @click="addExpense">Add expense</BaseButton>
        </template>

        <p v-if="(form.expenses ?? []).length === 0" class="muted no-expenses">No expenses on this invoice.</p>

        <div v-else class="expenses">
          <div v-for="(expense, index) in form.expenses" :key="index" class="expense-row">
            <input
              :id="`expense-description-${index}`"
              v-model="expense.description"
              type="text"
              placeholder="Description, e.g. Train to Milan"
              class="control"
              :class="{ invalid: submitted && !expense.description.trim() }"
              :aria-label="`Expense ${index + 1} description`"
            />
            <input
              :id="`expense-amount-${index}`"
              v-model.number="expense.amount"
              type="number"
              step="0.01"
              min="0"
              class="control num amount"
              :class="{ invalid: submitted && !(expense.amount > 0) }"
              :aria-label="`Expense ${index + 1} amount`"
            />
            <select :id="`expense-currency-${index}`" v-model="expense.currency" class="control currency" :aria-label="`Expense ${index + 1} currency`">
              <option v-for="code in currencies" :key="code">{{ code }}</option>
            </select>
            <BaseButton variant="ghost-danger" icon="trash" icon-only :aria-label="`Remove expense ${index + 1}`" title="Remove" @click="removeExpense(index)" />
          </div>
        </div>
      </BasePanel>

      <div class="summary-bar">
        <dl class="summary">
          <template v-if="form.invoiceType === InvoiceType.Monthly">
            <div><dt>Worked days</dt><dd>{{ totals.workedDays }}</dd></div>
            <div v-if="isDailyRate"><dt>Billable days</dt><dd>{{ totals.effectiveDays.toFixed(2) }}</dd></div>
            <div><dt>Hours</dt><dd>{{ totals.totalHours.toFixed(1) }}</dd></div>
            <div v-if="totals.publicHolidays || totals.unpaidLeave"><dt>Holiday · leave</dt><dd>{{ totals.publicHolidays }} · {{ totals.unpaidLeave }}</dd></div>
          </template>
          <div v-if="expensesTotal"><dt>Expenses</dt><dd>{{ expensesTotal }}</dd></div>
          <div class="estimate"><dt>Estimated subtotal</dt><dd>{{ estimatedAmount }}</dd></div>
        </dl>
        <div class="summary-actions">
          <span v-if="blocker" class="blocker">{{ blocker }}</span>
          <BaseButton :disabled="loading" @click="handleCancel">Cancel</BaseButton>
          <BaseButton type="submit" variant="primary" :loading="loading" :disabled="!!blocker">Generate invoice</BaseButton>
        </div>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch, toRef } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useInvoicesStore } from '@/stores/invoices'
import { useCustomersStore } from '@/stores/customers'
import { useRatesStore } from '@/stores/rates'
import { useProjectsStore } from '@/stores/projects'
import { useSettingsStore } from '@/stores/settings'
import { useMonthlyReportTemplatesStore } from '@/stores/monthlyReportTemplates'
import { InvoiceType, RateType, DayType } from '@/types'
import type { GenerateInvoiceDto, WorkDayDto } from '@/types'
import { useWorkMonth, localDateString, dayHours, billedHours } from '@/composables/useWorkMonth'
import MonthCalendar from '@/components/time/MonthCalendar.vue'
import DayInspector from '@/components/time/DayInspector.vue'
import TimesheetList from '@/components/time/TimesheetList.vue'
import { useToast } from '@/composables/useToast'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import { formatMoney, invoiceTypeLabel } from '@/utils/format'

const toast = useToast()

const router = useRouter()
const route = useRoute()
const invoicesStore = useInvoicesStore()
const customersStore = useCustomersStore()
const ratesStore = useRatesStore()
const projectsStore = useProjectsStore()
const settingsStore = useSettingsStore()
const monthlyReportTemplatesStore = useMonthlyReportTemplatesStore()

const loading = ref(false)
const selectedMonth = ref(new Date().getMonth() + 1)
const selectedYear = ref(new Date().getFullYear())
const selectedRate = ref<any>(null)
const noRate = ref(false)
const submitted = ref(false)
const currencies = ['EUR', 'USD', 'GBP', 'CHF']

const form = reactive<GenerateInvoiceDto>({
  customerId: 0,
  invoiceType: InvoiceType.Monthly,
  issueDate: localDateString(new Date()),
  workDays: [],
  expenses: [],
  monthlyReportTemplateId: undefined
})

const months = [
  { value: 1, label: 'January' },
  { value: 2, label: 'February' },
  { value: 3, label: 'March' },
  { value: 4, label: 'April' },
  { value: 5, label: 'May' },
  { value: 6, label: 'June' },
  { value: 7, label: 'July' },
  { value: 8, label: 'August' },
  { value: 9, label: 'September' },
  { value: 10, label: 'October' },
  { value: 11, label: 'November' },
  { value: 12, label: 'December' }
]

const years = computed(() => {
  const currentYear = new Date().getFullYear()
  return Array.from({ length: 5 }, (_, i) => currentYear - 2 + i)
})

const projectSuggestions = computed(() => {
  if (!form.customerId) return []
  return projectsStore
    .activeProjectsByCustomer(form.customerId)
    .map(p => p.name)
    .sort((a, b) => a.localeCompare(b))
})

// A customer with a single active project gets it pre-filled on new worked days
const defaultProjectName = computed(() => (projectSuggestions.value.length === 1 ? projectSuggestions.value[0] : ''))

const workMonth = useWorkMonth({
  workDays: toRef(form, 'workDays') as unknown as import('vue').Ref<WorkDayDto[]>,
  year: selectedYear,
  month: selectedMonth,
  firstDayOfWeek: () => settingsStore.getFirstDayOfWeek(),
  defaultProjectName: () => defaultProjectName.value
})
const totals = workMonth.totals

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

const isHourlyRate = computed(() => selectedRate.value?.type === RateType.Hourly)
const isDailyRate = computed(() => selectedRate.value?.type === RateType.Daily)

const estimatedAmount = computed(() => {
  if (!selectedRate.value || form.invoiceType !== InvoiceType.Monthly) {
    return '-'
  }

  const rate = selectedRate.value.price.amount
  const currency = selectedRate.value.price.currency

  if (selectedRate.value.type === RateType.Hourly) {
    return formatMoney(totals.value.totalHours * rate, currency)
  } else if (selectedRate.value.type === RateType.Daily) {
    return formatMoney(totals.value.effectiveDays * rate, currency)
  } else if (selectedRate.value.type === RateType.Monthly) {
    return formatMoney(rate, currency)
  }

  return '-'
})

const isFormValid = computed(() => {
  if (!form.customerId || form.invoiceType === undefined || form.invoiceType === null || !form.issueDate) {
    return false
  }

  if (form.invoiceType === InvoiceType.Monthly && (form.workDays ?? []).length === 0) {
    return false
  }

  // Hourly and daily billing need some hours on every worked day
  if (form.invoiceType === InvoiceType.Monthly && (isHourlyRate.value || isDailyRate.value)) {
    if (workMonth.workedDays.value.some(wd => (isHourlyRate.value ? dayHours(wd) : billedHours(wd)) <= 0)) {
      return false
    }
  }

  return true
})

const availableTemplates = computed(() => {
  if (!form.customerId) return []
  return monthlyReportTemplatesStore.templates.filter(
    t => t.customerId === form.customerId && invoiceTypeLabel(t.invoiceType) === 'Monthly'
  )
})

const activeTemplateName = computed(() => availableTemplates.value.find(t => t.isActive)?.name ?? '')

const sortedCustomers = computed(() => [...customersStore.customers].sort((a, b) => a.name.localeCompare(b.name)))

const periodLabel = computed(() =>
  new Date(selectedYear.value, selectedMonth.value - 1, 1).toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
)

const rateUnit = computed(() => {
  const type = selectedRate.value?.type
  return type === RateType.Hourly ? 'per hour' : type === RateType.Monthly ? 'per month' : 'per day'
})

function shiftMonth(offset: number) {
  const date = new Date(selectedYear.value, selectedMonth.value - 1 + offset, 1)
  if (!years.value.includes(date.getFullYear())) return
  selectedYear.value = date.getFullYear()
  selectedMonth.value = date.getMonth() + 1
}

// Sum per currency: an EUR invoice may carry a USD expense
const expensesTotal = computed(() => {
  const totals = new Map<string, number>()
  for (const e of form.expenses ?? []) {
    if (Number(e.amount) > 0) totals.set(e.currency, (totals.get(e.currency) ?? 0) + Number(e.amount))
  }
  return [...totals.entries()].map(([currency, amount]) => formatMoney(amount, currency)).join(' + ')
})

/** Why the invoice can't be generated yet, in words; null when it can. */
const blocker = computed((): string | null => {
  if (!form.customerId) return 'Select a customer.'
  if (noRate.value) return 'The customer needs a rate.'
  if (!form.issueDate) return 'Set the issue date.'
  if (form.invoiceType === InvoiceType.Monthly) {
    if ((form.workDays ?? []).length === 0) return 'Mark at least one day.'
    if (!isFormValid.value) return 'Every worked day needs more than 0 hours.'
  }
  return null
})

function fillWeekdays() {
  workMonth.fillWeekdays()
}

// A new period starts from a clean month with every weekday pre-filled
function resetMonth() {
  workMonth.clearMonth()
  workMonth.fillWeekdays()
}

watch([selectedMonth, selectedYear], resetMonth)

// Once the customer's single project is known, put it on rows that don't name a project yet
watch(defaultProjectName, name => {
  if (!name) return
  form.workDays?.forEach(wd => wd.projects?.forEach(p => { if (!p.projectName) p.projectName = name }))
})

watch(() => form.customerId, () => {
  loadCustomerData()
})

onMounted(async () => {
  resetMonth()
  await Promise.all([
    customersStore.fetchAll(),
    settingsStore.fetchInvoiceSettings()
  ])
  // Opened from a customer page ("New invoice"): start with that customer selected
  const preselected = Number(route.query.customerId)
  if (preselected && customersStore.customers.some(c => c.id === preselected)) {
    form.customerId = preselected
  }
})

async function loadCustomerData() {
  selectedRate.value = null
  noRate.value = false
  if (!form.customerId) return

  try {
    // Load rates
    await ratesStore.fetchByCustomerId(form.customerId)
    const rates = ratesStore.ratesByCustomer(form.customerId)
    
    
    if (rates.length === 0) {
      noRate.value = true
      return
    }

    // For Monthly invoices, try Daily rate first, then Monthly, then Hourly
    let matchingRate = rates.find(r => r.type === RateType.Daily)
    
    if (!matchingRate) {
      matchingRate = rates.find(r => r.type === RateType.Monthly)
    }
    
    if (!matchingRate) {
      matchingRate = rates.find(r => r.type === RateType.Hourly)
    }
    
    
    if (matchingRate) {
      selectedRate.value = matchingRate
    } else {
      // Fallback: use first available rate
      selectedRate.value = rates[0]
    }

    // Load the customer's projects for calendar suggestions
    await projectsStore.fetchByCustomerId(form.customerId, false)

    // Load monthly report templates for this customer
    if (form.invoiceType === InvoiceType.Monthly) {
      await monthlyReportTemplatesStore.fetchByCustomer(form.customerId)
    }
  } catch (error) {
    toast.failure('Could not load the customer’s rates and projects', error)
  }
}

function addExpense() {
  form.expenses?.push({
    description: '',
    amount: 0,
    currency: selectedRate.value?.price?.currency ?? 'EUR'
  })
  requestAnimationFrame(() => document.getElementById(`expense-description-${(form.expenses?.length ?? 1) - 1}`)?.focus())
}

function removeExpense(index: number) {
  form.expenses?.splice(index, 1)
}

async function handleSubmit() {
  submitted.value = true
  if (blocker.value) return
  const incomplete = (form.expenses ?? []).findIndex(e => !e.description.trim() || !(Number(e.amount) > 0))
  if (incomplete >= 0) {
    toast.warning('Complete or remove the unfinished expense', { message: 'Each expense needs a description and an amount above 0.' })
    document.getElementById(`expense-description-${incomplete}`)?.focus()
    return
  }

  try {
    loading.value = true

    const payload: GenerateInvoiceDto = {
      ...form,
      // Drop incomplete project rows; keep a day even if it ends up with no projects.
      // hoursWorked mirrors what the backend persists: the named projects' total when there are any,
      // otherwise the hours typed on unnamed rows (so partial days bill correctly without project names).
      workDays: (form.workDays ?? []).map(wd => {
        const rows = (wd.projects ?? []).filter(p => Number(p.hours) > 0)
        const projects = rows
          .filter(p => p.projectName.trim().length > 0)
          .map(p => ({ projectName: p.projectName.trim(), hours: Number(p.hours), projectId: p.projectId ?? null }))
        const hoursSource = projects.length > 0 ? projects : rows
        const isWorked = (wd.dayType ?? DayType.Worked) === DayType.Worked
        const hoursWorked = isWorked && hoursSource.length > 0
          ? hoursSource.reduce((sum, p) => sum + Number(p.hours), 0)
          : undefined
        const notes = wd.notes?.trim() ? wd.notes.trim() : undefined
        return { ...wd, hoursWorked, notes, projects }
      })
    }

    // Add year and month for monthly invoices
    if (form.invoiceType === InvoiceType.Monthly) {
      payload.year = selectedYear.value
      payload.month = selectedMonth.value
    }

    const invoice = await invoicesStore.generate(payload)
    router.push(`/invoices/${invoice.id}`)
  } catch (error: any) {
    toast.failure('Failed to generate invoice', error)
  } finally {
    loading.value = false
  }
}

function handleCancel() {
  router.push('/invoices')
}
</script>

<style scoped>
.invoice-generate {
  max-width: 82rem;
}

.generate-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1rem 1.1rem;
}

.span-2 {
  grid-column: span 2;
}

@media (max-width: 900px) {
  .grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 560px) {
  .grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .span-2 {
    grid-column: auto;
  }
}

.period {
  display: flex;
  gap: 0.4rem;
}

.period .control {
  flex: 1;
}

.period .year {
  flex: 0 0 6.5rem;
}

.rate-line {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.45rem;
  margin-top: 1rem;
  padding: 0.55rem 0.75rem;
  border-radius: var(--radius-md);
  background: var(--color-surface-muted);
  font-size: var(--text-md);
  color: var(--color-text-secondary);
}

.rate-line .app-icon {
  color: var(--color-primary);
}

.rate-line.missing {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}

.rate-line.missing .app-icon {
  color: var(--color-warning);
}

.rate-line strong {
  color: var(--color-text);
}

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

.no-expenses {
  margin: 0;
  font-size: var(--text-md);
}

.expenses {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.expense-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 9rem 6rem auto;
  gap: 0.5rem;
  align-items: center;
}

@media (max-width: 640px) {
  .expense-row {
    grid-template-columns: minmax(0, 1fr) 6rem auto;
  }

  .expense-row .control:first-child {
    grid-column: 1 / -1;
  }
}

/* sticks to the bottom of the viewport while the form scrolls */
.summary-bar {
  position: sticky;
  bottom: 0;
  z-index: 20;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem 1.5rem;
  margin: 0 -0.25rem;
  padding: 0.8rem 1.1rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: 0 -4px 18px rgba(20, 26, 38, 0.08);
}

.summary {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem 1.6rem;
  margin: 0;
}

.summary div {
  display: flex;
  flex-direction: column;
}

.summary dt {
  font-size: var(--text-xs);
  color: var(--color-text-muted);
}

.summary dd {
  margin: 0;
  font-size: var(--text-base);
  font-weight: 600;
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}

.summary .estimate dd {
  font-size: 1.15rem;
  color: var(--color-primary);
}

.summary-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.blocker {
  font-size: var(--text-sm);
  color: var(--color-warning);
}
</style>
