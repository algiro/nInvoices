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
              v-model="form.type"
              required
              class="form-control"
            >
              <option value="Monthly">Monthly</option>
              <option value="OneTime">One-Time</option>
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

        <div v-if="form.type === 'Monthly'" class="form-group">
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
      </div>

      <div v-if="form.type === 'Monthly'" class="form-section">
        <h3 class="section-title">Mark Worked Days</h3>
        
        <div class="calendar">
          <div class="calendar-header">
            <h4 class="text-lg font-semibold">
              {{ months.find(m => m.value === selectedMonth)?.label }} {{ selectedYear }}
            </h4>
            <p class="text-sm text-gray-600">Click on days to mark them as worked</p>
          </div>

          <div class="calendar-grid">
            <div class="day-header">Sun</div>
            <div class="day-header">Mon</div>
            <div class="day-header">Tue</div>
            <div class="day-header">Wed</div>
            <div class="day-header">Thu</div>
            <div class="day-header">Fri</div>
            <div class="day-header">Sat</div>

            <div
              v-for="day in calendarDays"
              :key="day.date"
              class="day-cell"
              :class="{
                'empty': !day.isCurrentMonth,
                'weekend': day.isWeekend,
                'worked': isWorkedDay(day.date),
                'today': isToday(day.date)
              }"
              @click="toggleWorkDay(day)"
            >
              <span v-if="day.isCurrentMonth">{{ day.day }}</span>
            </div>
          </div>

          <div class="calendar-summary">
            <div class="stat">
              <span class="stat-label">Total Worked Days:</span>
              <span class="stat-value">{{ form.workDays.length }}</span>
            </div>
            <div class="stat">
              <span class="stat-label">Estimated Amount:</span>
              <span class="stat-value">{{ estimatedAmount }}</span>
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
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useInvoicesStore } from '@/stores/invoices'
import { useCustomersStore } from '@/stores/customers'
import type { GenerateInvoiceDto, WorkDayDto, ExpenseDto } from '@/types'

const router = useRouter()
const invoicesStore = useInvoicesStore()
const customersStore = useCustomersStore()

const loading = ref(false)
const selectedMonth = ref(new Date().getMonth() + 1)
const selectedYear = ref(new Date().getFullYear())
const selectedRate = ref<any>(null)

const form = reactive<GenerateInvoiceDto>({
  customerId: 0,
  type: 'Monthly',
  issueDate: new Date().toISOString().split('T')[0],
  workDays: [],
  expenses: []
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

const calendarDays = computed((): CalendarDay[] => {
  const firstDay = new Date(selectedYear.value, selectedMonth.value - 1, 1)
  const lastDay = new Date(selectedYear.value, selectedMonth.value, 0)
  const startingDayOfWeek = firstDay.getDay()
  
  const days: CalendarDay[] = []
  
  for (let i = 0; i < startingDayOfWeek; i++) {
    const date = new Date(firstDay)
    date.setDate(date.getDate() - (startingDayOfWeek - i))
    days.push({
      date: date.toISOString().split('T')[0],
      day: date.getDate(),
      isCurrentMonth: false,
      isWeekend: date.getDay() === 0 || date.getDay() === 6
    })
  }
  
  for (let day = 1; day <= lastDay.getDate(); day++) {
    const date = new Date(selectedYear.value, selectedMonth.value - 1, day)
    days.push({
      date: date.toISOString().split('T')[0],
      day,
      isCurrentMonth: true,
      isWeekend: date.getDay() === 0 || date.getDay() === 6
    })
  }
  
  while (days.length % 7 !== 0) {
    const date = new Date(lastDay)
    date.setDate(date.getDate() + (days.length - lastDay.getDate() - startingDayOfWeek + 1))
    days.push({
      date: date.toISOString().split('T')[0],
      day: date.getDate(),
      isCurrentMonth: false,
      isWeekend: date.getDay() === 0 || date.getDay() === 6
    })
  }
  
  return days
})

const estimatedAmount = computed(() => {
  if (!selectedRate.value || form.type !== 'Monthly') {
    return '-'
  }
  
  const workedDays = form.workDays.length
  const rate = selectedRate.value.price.amount
  const currency = selectedRate.value.price.currency
  
  if (selectedRate.value.type === 'Daily') {
    return `${(workedDays * rate).toFixed(2)} ${currency}`
  } else if (selectedRate.value.type === 'Monthly') {
    return `${rate.toFixed(2)} ${currency}`
  }
  
  return '-'
})

const isFormValid = computed(() => {
  if (!form.customerId || !form.type || !form.issueDate) {
    return false
  }
  
  if (form.type === 'Monthly' && form.workDays.length === 0) {
    return false
  }
  
  return true
})

watch([selectedMonth, selectedYear], () => {
  form.workDays = []
})

onMounted(() => {
  customersStore.fetchAll()
})

async function loadCustomerData() {
  if (!form.customerId) return
  
  selectedRate.value = {
    type: 'Daily',
    price: { amount: 300, currency: 'EUR' }
  }
}

function isWorkedDay(date: string): boolean {
  return form.workDays.some(wd => wd.date === date)
}

function isToday(date: string): boolean {
  return date === new Date().toISOString().split('T')[0]
}

function toggleWorkDay(day: CalendarDay) {
  if (!day.isCurrentMonth) return
  
  const existingIndex = form.workDays.findIndex(wd => wd.date === day.date)
  
  if (existingIndex >= 0) {
    form.workDays.splice(existingIndex, 1)
  } else {
    form.workDays.push({
      date: day.date,
      hoursWorked: 8,
      description: null
    })
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
    const invoice = await invoicesStore.generate(form)
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

.day-cell {
  aspect-ratio: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 2px solid #e5e7eb;
  border-radius: 0.5rem;
  cursor: pointer;
  transition: all 0.2s;
  font-weight: 500;
  background: white;
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
  background: #2563eb;
  color: white;
  border-color: #1d4ed8;
}

.day-cell.today {
  border-color: #f59e0b;
  font-weight: 700;
}

.calendar-summary {
  display: flex;
  justify-content: space-between;
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