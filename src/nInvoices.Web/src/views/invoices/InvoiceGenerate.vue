<template>
  <div class="invoice-generate">
    <div class="header">
      <h1 class="text-3xl font-bold">Generate Invoice</h1>
    </div>

    <form @submit.prevent="handleSubmit" class="generate-form">
      <div class="form-section">
        <h3 class="section-title">Invoice Information</h3>
        
        <div class="form-group">
          <label for="customer" class="form-label">
            Customer <span class="text-red-500">*</span>
          </label>
          <select
            id="customer"
            v-model="form.customerId"
            required
            class="form-control"
            @change="loadCustomerData"
          >
            <option value="">Select a customer</option>
            <option
              v-for="customer in customersStore.customers"
              :key="customer.id"
              :value="customer.id"
            >
              {{ customer.name }}
            </option>
          </select>
        </div>

        <div class="grid grid-cols-2 gap-4">
          <div class="form-group">
            <label for="type" class="form-label">
              Invoice Type <span class="text-red-500">*</span>
            </label>
            <select
              id="type"
              v-model="form.invoiceType"
              required
              class="form-control"
            >
              <option :value="InvoiceType.Monthly">Monthly</option>
              <option :value="InvoiceType.OneTime">One-Time</option>
            </select>
          </div>

          <div class="form-group">
            <label for="issueDate" class="form-label">
              Issue Date <span class="text-red-500">*</span>
            </label>
            <input
              id="issueDate"
              v-model="form.issueDate"
              type="date"
              required
              class="form-control"
            />
          </div>
        </div>

        <div v-if="form.invoiceType === InvoiceType.Monthly" class="form-group">
          <label class="form-label">
            Select Month & Year <span class="text-red-500">*</span>
          </label>
          <div class="grid grid-cols-2 gap-4">
            <select v-model="selectedMonth" class="form-control" required>
              <option v-for="month in months" :key="month.value" :value="month.value">
                {{ month.label }}
              </option>
            </select>
            <select v-model="selectedYear" class="form-control" required>
              <option v-for="year in years" :key="year" :value="year">
                {{ year }}
              </option>
            </select>
          </div>
        </div>

        <div v-if="form.invoiceType === InvoiceType.Monthly && availableTemplates.length > 0" class="form-group">
          <label for="monthlyReportTemplate" class="form-label">
            Monthly Report Template
          </label>
          <select
            id="monthlyReportTemplate"
            v-model="form.monthlyReportTemplateId"
            class="form-control"
          >
            <option :value="undefined">Use active template (default)</option>
            <option
              v-for="template in availableTemplates"
              :key="template.id"
              :value="template.id"
            >
              {{ template.name }} {{ template.isActive ? '(Active)' : '' }}
            </option>
          </select>
          <p class="text-xs text-gray-500 mt-1">
            Select which template to use for the monthly report PDF. If not selected, the active template will be used.
          </p>
        </div>
      </div>

      <div v-if="form.invoiceType === InvoiceType.Monthly" class="form-section">
        <div class="time-header">
          <div>
            <h3 class="section-title">Worked Days</h3>
            <p class="section-help">
              <template v-if="timeView === 'calendar'">
                Weekdays start as full worked days. Select the days that differ and edit them on the right.
              </template>
              <template v-else>
                Edit any day inline. Click a day number to select it (Shift for a range) and split it across projects on the right.
              </template>
            </p>
          </div>
          <div class="time-actions">
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
            <button type="button" class="btn-secondary btn-sm" @click="fillWeekdays">Fill weekdays (8h)</button>
            <button type="button" class="btn-secondary btn-sm" @click="workMonth.clearMonth">Clear month</button>
          </div>
        </div>

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

        <div class="calendar-summary">
          <div class="stat">
            <span class="stat-label">Worked Days</span>
            <span class="stat-value">{{ totals.workedDays }}</span>
          </div>
          <div v-if="isDailyRate" class="stat">
            <span class="stat-label">Effective Days</span>
            <span class="stat-value">{{ totals.effectiveDays.toFixed(2) }}</span>
          </div>
          <div class="stat">
            <span class="stat-label">Total Hours</span>
            <span class="stat-value">{{ totals.totalHours.toFixed(1) }}h</span>
          </div>
          <div class="stat">
            <span class="stat-label">Holidays · Leave</span>
            <span class="stat-value">{{ totals.publicHolidays }} · {{ totals.unpaidLeave }}</span>
          </div>
          <div class="stat">
            <span class="stat-label">Notes</span>
            <span class="stat-value">{{ totals.notes }}</span>
          </div>
          <div class="stat">
            <span class="stat-label">Estimated Amount</span>
            <span class="stat-value">{{ estimatedAmount }}</span>
          </div>
        </div>
      </div>

      <div class="form-section">
        <div class="flex justify-between items-center mb-4">
          <h3 class="section-title mb-0">Expenses</h3>
          <button type="button" @click="addExpense" class="btn-secondary">
            Add Expense
          </button>
        </div>

        <div v-if="form.expenses.length === 0" class="text-center py-8 text-gray-500">
          No expenses added yet
        </div>

        <div v-else class="expenses-list">
          <div
            v-for="(expense, index) in form.expenses"
            :key="index"
            class="expense-row"
          >
            <input
              v-model="expense.description"
              type="text"
              placeholder="Description"
              class="form-control flex-1"
              required
            />
            <input
              v-model.number="expense.amount"
              type="number"
              step="0.01"
              placeholder="Amount"
              class="form-control w-32"
              required
            />
            <select v-model="expense.currency" class="form-control w-24">
              <option>EUR</option>
              <option>USD</option>
              <option>GBP</option>
            </select>
            <button
              type="button"
              @click="removeExpense(index)"
              class="btn-icon text-red-600"
              title="Remove expense"
            >
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>
      </div>

      <div class="form-actions">
        <button
          type="button"
          @click="handleCancel"
          class="btn-secondary"
          :disabled="loading"
        >
          Cancel
        </button>
        <button
          type="submit"
          class="btn-primary"
          :disabled="loading || !isFormValid"
        >
          {{ loading ? 'Generating...' : 'Generate Invoice' }}
        </button>
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
    return `${(totals.value.totalHours * rate).toFixed(2)} ${currency}`
  } else if (selectedRate.value.type === RateType.Daily) {
    return `${(totals.value.effectiveDays * rate).toFixed(2)} ${currency}`
  } else if (selectedRate.value.type === RateType.Monthly) {
    return `${rate.toFixed(2)} ${currency}`
  }

  return '-'
})

const isFormValid = computed(() => {
  if (!form.customerId || form.invoiceType === undefined || form.invoiceType === null || !form.issueDate) {
    return false
  }

  if (form.invoiceType === InvoiceType.Monthly && form.workDays.length === 0) {
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
    t => t.customerId === form.customerId && t.invoiceType === InvoiceType.Monthly
  )
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
  if (!form.customerId) return

  try {
    // Load rates
    await ratesStore.fetchByCustomerId(form.customerId)
    const rates = ratesStore.ratesByCustomer(form.customerId)
    
    console.log('Fetched rates for customer:', form.customerId, rates)
    
    if (rates.length === 0) {
      toast.warning('This customer has no rates', { message: 'Add a rate on the customer page before generating an invoice.' })
      selectedRate.value = null
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
    
    console.log('Selected rate:', matchingRate)
    
    if (matchingRate) {
      selectedRate.value = matchingRate
    } else {
      // Fallback: use first available rate
      selectedRate.value = rates[0]
      console.warn('No Daily/Monthly/Hourly rate found, using first available rate', rates[0])
    }

    // Load the customer's projects for calendar suggestions
    await projectsStore.fetchByCustomerId(form.customerId, false)

    // Load monthly report templates for this customer
    if (form.invoiceType === InvoiceType.Monthly) {
      await monthlyReportTemplatesStore.fetchByCustomer(form.customerId)
    }
  } catch (error) {
    console.error('Failed to load customer data:', error)
    selectedRate.value = null
  }
}

function addExpense() {
  form.expenses.push({
    description: '',
    amount: 0,
    currency: 'EUR'
  })
}

function removeExpense(index: number) {
  form.expenses.splice(index, 1)
}

async function handleSubmit() {
  if (!isFormValid.value) return

  try {
    loading.value = true

    const payload: GenerateInvoiceDto = {
      ...form,
      // Drop incomplete project rows; keep a day even if it ends up with no projects.
      // hoursWorked mirrors what the backend persists: the named projects' total when there are any,
      // otherwise the hours typed on unnamed rows (so partial days bill correctly without project names).
      workDays: form.workDays.map(wd => {
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
  padding: 2rem;
  max-width: 1200px;
  margin: 0 auto;
}

.header {
  margin-bottom: 2rem;
}

.generate-form {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.form-section {
  background: white;
  padding: 2rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.section-title {
  font-size: 1.25rem;
  font-weight: 600;
  margin-bottom: 1.5rem;
  color: #1f2937;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-label {
  display: block;
  font-size: 0.875rem;
  font-weight: 500;
  color: #374151;
  margin-bottom: 0.5rem;
}

.form-control {
  width: 100%;
  padding: 0.75rem 1rem;
  border: 1px solid #d1d5db;
  border-radius: 0.375rem;
  font-size: 1rem;
}

.form-control:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}


.section-help {
  margin: 0.25rem 0 0;
  font-size: 0.875rem;
  color: #6b7280;
}

.time-header {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 1.25rem;
}

.time-header .section-title {
  margin-bottom: 0;
}

.time-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.view-switch {
  display: inline-flex;
  gap: 2px;
  padding: 2px;
  border: 1px solid #d1d5db;
  border-radius: 0.4rem;
  background: #ffffff;
}

.view-switch button {
  padding: 0.3rem 0.75rem;
  border: 0;
  border-radius: 0.3rem;
  background: transparent;
  color: #374151;
  font: inherit;
  font-size: 0.85rem;
  line-height: 1.3;
  cursor: pointer;
}

.view-switch button:hover {
  background: #f3f4f6;
}

.view-switch button.active {
  background: #2563eb;
  color: #ffffff;
  font-weight: 600;
}

.btn-sm {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}

.time-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 20rem;
  gap: 1rem;
  align-items: start;
}

@media (max-width: 900px) {
  .time-layout {
    grid-template-columns: 1fr;
  }
}

.calendar-summary {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 1rem;
  margin-top: 1.5rem;
  padding-top: 1.5rem;
  border-top: 1px solid #e5e7eb;
}

.stat {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.stat-label {
  font-size: 0.875rem;
  color: #6b7280;
}

.stat-value {
  font-size: 1.5rem;
  font-weight: 700;
  color: #1f2937;
}

.expenses-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.expense-row {
  display: flex;
  gap: 1rem;
  align-items: center;
}

.btn-icon {
  padding: 0.5rem;
  border: none;
  background: transparent;
  border-radius: 0.375rem;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-icon:hover {
  background: #fee2e2;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #e5e7eb;
}

.btn-primary,
.btn-secondary {
  padding: 0.75rem 1.5rem;
  border-radius: 0.375rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
}

.btn-primary {
  background: #2563eb;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #1d4ed8;
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: white;
  color: #374151;
  border: 1px solid #d1d5db;
}

.btn-secondary:hover {
  background: #f9fafb;
}
</style>