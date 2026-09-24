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
        <h3 class="section-title">Mark Worked Days</h3>
        
        <div class="calendar">
          <div class="calendar-header">
            <h4 class="text-lg font-semibold">
              {{ months.find(m => m.value === selectedMonth)?.label }} {{ selectedYear }}
            </h4>
            <p class="text-sm text-gray-600">Click on days to cycle through: Worked → Public Holiday → Unpaid Leave → Unselected<span v-if="isDailyRate"> · Double-click a worked day to set partial hours</span></p>
            <div class="day-type-legend">
              <div class="legend-item">
                <span class="legend-color worked"></span>
                <span>Worked</span>
              </div>
              <div class="legend-item">
                <span class="legend-color public-holiday"></span>
                <span>Public Holiday</span>
              </div>
              <div class="legend-item">
                <span class="legend-color unpaid-leave"></span>
                <span>Unpaid Leave</span>
              </div>
            </div>
          </div>

          <div class="calendar-grid">
            <div v-for="dayName in dayHeaders" :key="dayName" class="day-header">
              {{ dayName }}
            </div>

            <div
              v-for="day in calendarDays"
              :key="day.date"
              class="day-cell-wrapper"
            >
              <div
                class="day-cell"
                :class="{
                  'empty': !day.isCurrentMonth,
                  'weekend': day.isWeekend,
                  'worked': getDayType(day.date) === DayType.Worked,
                  'public-holiday': getDayType(day.date) === DayType.PublicHoliday,
                  'unpaid-leave': getDayType(day.date) === DayType.UnpaidLeave,
                  'today': isToday(day.date),
                  'partial': getWorkDayPartialHours(day.date) !== null
                }"
                @click="handleDayClick(day)"
                @dblclick.stop="handleDayDblClick(day)"
              >
                <span v-if="day.isCurrentMonth">{{ day.day }}</span>
                <span v-if="day.isCurrentMonth && getWorkDayPartialHours(day.date) !== null" class="partial-badge">
                  {{ getWorkDayPartialHours(day.date) }}h
                </span>
              </div>
              <div v-if="popoverDate === day.date" class="hours-popover" @click.stop>
                <div class="popover-title">Partial day</div>
                <input
                  v-model.number="popoverHours"
                  type="number"
                  step="0.5"
                  min="0.5"
                  max="8"
                  class="popover-input"
                  @keyup.enter="confirmHoursPopover"
                  @keyup.escape="closeHoursPopover"
                />
                <div class="popover-footer">
                  <button type="button" @click="confirmHoursPopover" class="btn-popover-primary">Done</button>
                  <button type="button" @click="closeHoursPopover" class="btn-popover-cancel">Cancel</button>
                </div>
              </div>
            </div>
          </div>

          <div class="calendar-summary">
            <div class="stat">
              <span class="stat-label">Worked Days:</span>
              <span class="stat-value">{{ workedDaysCount }}</span>
            </div>
            <div class="stat">
              <span class="stat-label">Public Holidays:</span>
              <span class="stat-value">{{ publicHolidaysCount }}</span>
            </div>
            <div class="stat">
              <span class="stat-label">Unpaid Leave:</span>
              <span class="stat-value">{{ unpaidLeaveCount }}</span>
            </div>
            <div class="stat">
              <span class="stat-label">Total Hours:</span>
              <span class="stat-value">{{ totalHours.toFixed(1) }}h</span>
            </div>
            <div v-if="hasPartialDays" class="stat">
              <span class="stat-label">Effective Days:</span>
              <span class="stat-value">{{ effectiveDays.toFixed(2) }}</span>
            </div>
            <div class="stat">
              <span class="stat-label">Estimated Amount:</span>
              <span class="stat-value">{{ estimatedAmount }}</span>
            </div>
          </div>

          <!-- Per-day project time breakdown -->
          <div v-if="workedDaysCount > 0" class="worked-days-section">
            <h4 class="text-md font-semibold mb-1">Projects per Day</h4>
            <p class="text-sm text-gray-600 mb-3">
              Split each worked day across one or more projects. New project names are saved
              automatically.
              <span v-if="isHourlyRate"> Hours drive the hourly billing.</span>
            </p>

            <datalist id="project-suggestions">
              <option v-for="p in projectSuggestions" :key="p" :value="p" />
            </datalist>

            <div
              v-for="workDay in sortedWorkedDays"
              :key="workDay.date"
              class="worked-day-card"
            >
              <div class="worked-day-header">
                <span class="worked-day-date">{{ formatDate(workDay.date) }}</span>
                <span
                  class="worked-day-total"
                  :class="{ 'text-red-500': isHourlyRate && dayHours(workDay) <= 0 }"
                >
                  {{ dayHours(workDay).toFixed(1) }}h
                </span>
              </div>

              <div
                v-for="(alloc, index) in (workDay.projects ?? [])"
                :key="index"
                class="alloc-row"
              >
                <input
                  v-model.trim="alloc.projectName"
                  type="text"
                  list="project-suggestions"
                  placeholder="Project name"
                  class="form-control alloc-name"
                />
                <input
                  v-model.number="alloc.hours"
                  type="number"
                  step="0.5"
                  min="0"
                  max="24"
                  placeholder="Hours"
                  class="form-control alloc-hours"
                />
                <button
                  type="button"
                  class="btn-icon text-red-600"
                  title="Remove project"
                  @click="removeAllocation(workDay, index)"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>

              <button type="button" class="btn-link" @click="addAllocation(workDay)">
                + Add project
              </button>
            </div>
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
import { ref, reactive, computed, onMounted, watch, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { useInvoicesStore } from '@/stores/invoices'
import { useCustomersStore } from '@/stores/customers'
import { useRatesStore } from '@/stores/rates'
import { useProjectsStore } from '@/stores/projects'
import { useSettingsStore } from '@/stores/settings'
import { useMonthlyReportTemplatesStore } from '@/stores/monthlyReportTemplates'
import { InvoiceType, RateType, DayType, DayTypeNames } from '@/types'
import type { GenerateInvoiceDto, WorkDayDto, ExpenseDto } from '@/types'

const router = useRouter()
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
const popoverDate = ref<string | null>(null)
const popoverHours = ref<number>(8)
let pendingClickTimer: ReturnType<typeof setTimeout> | null = null

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

interface CalendarDay {
  date: string
  day: number
  isCurrentMonth: boolean
  isWeekend: boolean
}

// Use local date components to avoid UTC-offset shifting when calling toISOString()
function localDateString(date: Date): string {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

const dayHeaders= computed(() => {
  const allDays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
  const firstDayOfWeek = settingsStore.getFirstDayOfWeek()
  return [...allDays.slice(firstDayOfWeek), ...allDays.slice(0, firstDayOfWeek)]
})

const calendarDays = computed((): CalendarDay[] => {
  const firstDay = new Date(selectedYear.value, selectedMonth.value - 1, 1)
  const lastDay = new Date(selectedYear.value, selectedMonth.value, 0)
  const firstDayOfWeek = settingsStore.getFirstDayOfWeek() // 0 = Sunday, 1 = Monday, etc.
  
  // Adjust starting day to match configured first day of week
  let startingDayOfWeek = firstDay.getDay() - firstDayOfWeek
  if (startingDayOfWeek < 0) startingDayOfWeek += 7
  
  const days: CalendarDay[] = []
  
  for (let i = 0; i < startingDayOfWeek; i++) {
    const date = new Date(firstDay)
    date.setDate(date.getDate() - (startingDayOfWeek - i))
    days.push({
      date: localDateString(date),
      day: date.getDate(),
      isCurrentMonth: false,
      isWeekend: date.getDay() === 0 || date.getDay() === 6
    })
  }
  
  for (let day = 1; day <= lastDay.getDate(); day++) {
    const date = new Date(selectedYear.value, selectedMonth.value - 1, day)
    days.push({
      date: localDateString(date),
      day,
      isCurrentMonth: true,
      isWeekend: date.getDay() === 0 || date.getDay() === 6
    })
  }
  
  while (days.length % 7 !== 0) {
    const date = new Date(lastDay)
    date.setDate(date.getDate() + (days.length - lastDay.getDate() - startingDayOfWeek + 1))
    days.push({
      date: localDateString(date),
      day: date.getDate(),
      isCurrentMonth: false,
      isWeekend: date.getDay() === 0 || date.getDay() === 6
    })
  }
  
  return days
})

const workedDaysCount = computed(() => {
  return form.workDays.filter(wd => (wd.dayType ?? DayType.Worked) === DayType.Worked).length
})

const publicHolidaysCount = computed(() => {
  return form.workDays.filter(wd => wd.dayType === DayType.PublicHoliday).length
})

const unpaidLeaveCount = computed(() => {
  return form.workDays.filter(wd => wd.dayType === DayType.UnpaidLeave).length
})

const isHourlyRate = computed(() => selectedRate.value?.type === RateType.Hourly)
const isDailyRate = computed(() => selectedRate.value?.type === RateType.Daily)

function dayHours(workDay: WorkDayDto): number {
  return (workDay.projects ?? []).reduce((sum, p) => sum + (Number(p.hours) || 0), 0)
}

const sortedWorkedDays = computed(() =>
  form.workDays
    .filter(wd => (wd.dayType ?? DayType.Worked) === DayType.Worked)
    .sort((a, b) => a.date.localeCompare(b.date))
)

const projectSuggestions = computed(() => {
  if (!form.customerId) return []
  return projectsStore
    .activeProjectsByCustomer(form.customerId)
    .map(p => p.name)
    .sort((a, b) => a.localeCompare(b))
})

const totalHours = computed(() => {
  return sortedWorkedDays.value.reduce((sum, wd) => sum + dayHours(wd), 0)
})

const effectiveDays = computed(() => {
  return form.workDays
    .filter(wd => (wd.dayType ?? DayType.Worked) === DayType.Worked)
    .reduce((sum, wd) => sum + (wd.hoursWorked != null ? wd.hoursWorked / 8 : 1), 0)
})

const hasPartialDays = computed(() => {
  return isDailyRate.value && form.workDays.some(
    wd => (wd.dayType ?? DayType.Worked) === DayType.Worked &&
          wd.hoursWorked != null && wd.hoursWorked !== 8
  )
})
const estimatedAmount = computed(() => {
  if (!selectedRate.value || form.invoiceType !== InvoiceType.Monthly) {
    return '-'
  }

  const rate = selectedRate.value.price.amount
  const currency = selectedRate.value.price.currency

  if (selectedRate.value.type === RateType.Hourly) {
    const hours = totalHours.value
    return `${(hours * rate).toFixed(2)} ${currency} (${hours.toFixed(1)}h)`
  } else if (selectedRate.value.type === RateType.Daily) {
    const days = effectiveDays.value
    return `${(days * rate).toFixed(2)} ${currency}`
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

  // Hourly billing needs at least some hours on every worked day
  if (form.invoiceType === InvoiceType.Monthly && isHourlyRate.value) {
    if (sortedWorkedDays.value.some(wd => dayHours(wd) <= 0)) {
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

watch([selectedMonth, selectedYear], () => {
  form.workDays = []
})

watch(() => form.customerId, () => {
  loadCustomerData()
})

onMounted(async () => {
  await Promise.all([
    customersStore.fetchAll(),
    settingsStore.fetchInvoiceSettings()
  ])
})

async function loadCustomerData() {
  if (!form.customerId) return

  try {
    // Load rates
    await ratesStore.fetchByCustomerId(form.customerId)
    const rates = ratesStore.ratesByCustomer(form.customerId)
    
    console.log('Fetched rates for customer:', form.customerId, rates)
    
    if (rates.length === 0) {
      alert('No rates found for this customer. Please add rates first.')
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

function getDayType(date: string): DayType | null {
  const workDay = form.workDays.find(wd => wd.date === date)
  return workDay ? (workDay.dayType ?? DayType.Worked) : null
}

function getWorkDayPartialHours(date: string): number | null {
  if (!isDailyRate.value) return null
  const wd = form.workDays.find(w => w.date === date)
  if (!wd || (wd.dayType ?? DayType.Worked) !== DayType.Worked) return null
  return wd.hoursWorked ?? null
}

function isToday(date: string): boolean {
  return date === localDateString(new Date())
}

function formatDate(dateStr: string): string {
  const date = new Date(dateStr + 'T00:00:00')
  return date.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' })
}

function cycleWorkDayType(day: CalendarDay) {
  if (!day.isCurrentMonth) return

  const existingIndex = form.workDays.findIndex(wd => wd.date === day.date)

  if (existingIndex >= 0) {
    const currentType = form.workDays[existingIndex].dayType ?? DayType.Worked

    // Cycle: Worked → PublicHoliday → UnpaidLeave → unselected
    if (currentType === DayType.Worked) {
      form.workDays[existingIndex].dayType = DayType.PublicHoliday
      form.workDays[existingIndex].hoursWorked = undefined // Clear hours when not worked
      form.workDays[existingIndex].projects = [] // Clear project time when not worked
    } else if (currentType === DayType.PublicHoliday) {
      form.workDays[existingIndex].dayType = DayType.UnpaidLeave
    } else {
      form.workDays.splice(existingIndex, 1)
    }
  } else {
    // First click: add as Worked day with a single 8h project row to fill in
    form.workDays.push({
      date: day.date,
      dayType: DayType.Worked,
      projects: [{ projectName: '', hours: 8 }]
    })
  }
}

function addAllocation(workDay: WorkDayDto) {
  if (!workDay.projects) workDay.projects = []
  workDay.projects.push({ projectName: '', hours: 0 })
}

function removeAllocation(workDay: WorkDayDto, index: number) {
  workDay.projects?.splice(index, 1)
}

function handleDayClick(day: CalendarDay) {
  if (!day.isCurrentMonth) return
  if (pendingClickTimer) return // Second click of a dblclick — let dblclick handler take over
  pendingClickTimer = setTimeout(() => {
    pendingClickTimer = null
    cycleWorkDayType(day)
  }, 250)
}

function handleDayDblClick(day: CalendarDay) {
  if (!day.isCurrentMonth || !isDailyRate.value) return
  if (pendingClickTimer) {
    clearTimeout(pendingClickTimer)
    pendingClickTimer = null
  }
  const existingIndex = form.workDays.findIndex(w => w.date === day.date)
  if (existingIndex >= 0 && (form.workDays[existingIndex].dayType ?? DayType.Worked) !== DayType.Worked) {
    return // Only open for worked days
  }
  if (existingIndex < 0) {
    form.workDays.push({ date: day.date, dayType: DayType.Worked, hoursWorked: undefined })
  }
  popoverHours.value = form.workDays.find(w => w.date === day.date)?.hoursWorked ?? 8
  popoverDate.value = day.date
  nextTick(() => {
    const input = document.querySelector<HTMLInputElement>('.popover-input')
    input?.focus()
    input?.select()
  })
}

function confirmHoursPopover() {
  if (popoverDate.value) {
    const idx = form.workDays.findIndex(w => w.date === popoverDate.value)
    if (idx >= 0) {
      const hours = Math.min(8, Math.max(0.5, popoverHours.value || 8))
      form.workDays[idx].hoursWorked = hours === 8 ? undefined : hours
    }
  }
  popoverDate.value = null
}

function closeHoursPopover() {
  popoverDate.value = null
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
      // Drop incomplete project rows; keep a day even if it ends up with no projects
      workDays: form.workDays.map(wd => ({
        ...wd,
        hoursWorked: undefined,
        projects: (wd.projects ?? [])
          .filter(p => p.projectName.trim().length > 0 && Number(p.hours) > 0)
          .map(p => ({ projectName: p.projectName.trim(), hours: Number(p.hours), projectId: p.projectId ?? null }))
      }))
    }

    // Add year and month for monthly invoices
    if (form.invoiceType === InvoiceType.Monthly) {
      payload.year = selectedYear.value
      payload.month = selectedMonth.value
    }

    const invoice = await invoicesStore.generate(payload)
    router.push(`/invoices/${invoice.id}`)
  } catch (error: any) {
    alert(`Failed to generate invoice: ${error.message}`)
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

.calendar {
  margin-top: 1rem;
}

.calendar-header {
  margin-bottom: 1.5rem;
}

.day-type-legend {
  display: flex;
  gap: 1.5rem;
  margin-top: 0.75rem;
  padding: 0.75rem;
  background: #f9fafb;
  border-radius: 0.5rem;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.875rem;
}

.legend-color {
  width: 1.5rem;
  height: 1.5rem;
  border-radius: 0.25rem;
  border: 1px solid #d1d5db;
}

.legend-color.worked {
  background: #e8f5e9;
  border-color: #4caf50;
}

.legend-color.public-holiday {
  background: #fff3e0;
  border-color: #ff9800;
}

.legend-color.unpaid-leave {
  background: #ffebee;
  border-color: #f44336;
}

.calendar-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 0.5rem;
}

.day-header {
  padding: 0.75rem;
  text-align: center;
  font-weight: 600;
  color: #6b7280;
  font-size: 0.875rem;
  text-transform: uppercase;
}

.day-cell-wrapper {
  position: relative;
  aspect-ratio: 1;
}

.day-cell {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
  border: 2px solid #e5e7eb;
  border-radius: 0.5rem;
  cursor: pointer;
  transition: all 0.2s;
  font-weight: 500;
  background: white;
}

.partial-badge {
  font-size: 0.6rem;
  font-weight: 700;
  opacity: 0.85;
  line-height: 1;
}

.hours-popover {
  position: absolute;
  z-index: 100;
  top: calc(100% + 4px);
  left: 50%;
  transform: translateX(-50%);
  background: white;
  border: 1px solid #d1d5db;
  border-radius: 0.5rem;
  padding: 0.625rem;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  width: 140px;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.popover-title {
  font-size: 0.7rem;
  font-weight: 600;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.popover-input {
  width: 100%;
  padding: 0.375rem 0.5rem;
  border: 1px solid #d1d5db;
  border-radius: 0.375rem;
  font-size: 0.875rem;
  text-align: center;
  outline: none;
}

.popover-input:focus {
  border-color: #2563eb;
  box-shadow: 0 0 0 2px rgba(37, 99, 235, 0.15);
}

.popover-footer {
  display: flex;
  gap: 0.375rem;
  justify-content: flex-end;
}

.btn-popover-primary {
  padding: 0.2rem 0.6rem;
  background: #2563eb;
  color: white;
  border: none;
  border-radius: 0.25rem;
  font-size: 0.75rem;
  cursor: pointer;
  font-weight: 500;
}

.btn-popover-primary:hover {
  background: #1d4ed8;
}

.btn-popover-cancel {
  padding: 0.2rem 0.6rem;
  background: white;
  color: #374151;
  border: 1px solid #d1d5db;
  border-radius: 0.25rem;
  font-size: 0.75rem;
  cursor: pointer;
  font-weight: 500;
}

.btn-popover-cancel:hover {
  background: #f9fafb;
}

.day-cell:hover:not(.empty) {
  border-color: #2563eb;
  background: #eff6ff;
}

.day-cell.empty {
  border-color: transparent;
  color: #d1d5db;
  cursor: default;
}

.day-cell.weekend:not(.empty) {
  background: #f9fafb;
}

.day-cell.worked {
  background: #e8f5e9;
  color: #1b5e20;
  border-color: #4caf50;
  font-weight: 600;
}

.day-cell.public-holiday {
  background: #fff3e0;
  color: #e65100;
  border-color: #ff9800;
  font-weight: 600;
}

.day-cell.unpaid-leave {
  background: #ffebee;
  color: #b71c1c;
  border-color: #f44336;
  font-weight: 600;
}

.day-cell.today {
  border-color: #f59e0b;
  font-weight: 700;
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

.worked-days-section {
  margin-top: 1.5rem;
  padding-top: 1.5rem;
  border-top: 1px solid #e5e7eb;
}

.worked-day-card {
  padding: 0.75rem;
  margin-bottom: 0.75rem;
  background: #f9fafb;
  border: 1px solid #e5e7eb;
  border-radius: 0.375rem;
}

.worked-day-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.worked-day-date {
  font-size: 0.875rem;
  font-weight: 600;
  color: #374151;
}

.worked-day-total {
  font-size: 0.875rem;
  font-weight: 600;
  color: #6b7280;
}

.alloc-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.alloc-name {
  flex: 1;
  padding: 0.375rem 0.5rem;
  font-size: 0.875rem;
}

.alloc-hours {
  width: 90px;
  padding: 0.375rem 0.5rem;
  font-size: 0.875rem;
}

.btn-link {
  background: none;
  border: none;
  color: #2563eb;
  font-size: 0.875rem;
  font-weight: 500;
  cursor: pointer;
  padding: 0.25rem 0;
}

.btn-link:hover {
  text-decoration: underline;
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