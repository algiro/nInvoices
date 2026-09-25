import { ref, reactive, computed, watch, toRef, type Ref } from 'vue'
import { useCustomersStore } from '@/stores/customers'
import { useRatesStore } from '@/stores/rates'
import { useProjectsStore } from '@/stores/projects'
import { useSettingsStore } from '@/stores/settings'
import { useMonthlyReportTemplatesStore } from '@/stores/monthlyReportTemplates'
import { InvoiceType, RateType, DayType } from '@/types'
import type { GenerateInvoiceDto, WorkDayDto } from '@/types'
import { useWorkMonth, localDateString, dayHours, billedHours } from '@/composables/useWorkMonth'
import { useToast } from '@/composables/useToast'
import { formatMoney, invoiceTypeLabel } from '@/utils/format'

export const EXPENSE_CURRENCIES = ['EUR', 'USD', 'GBP', 'CHF']

/**
 * The invoice being built on the "New invoice" wizard: customer, period, worked days and
 * expenses, plus what each step still needs before the invoice can be generated.
 * Every step component receives the same instance.
 */
export function useInvoiceDraft() {
  const toast = useToast()
  const customersStore = useCustomersStore()
  const ratesStore = useRatesStore()
  const projectsStore = useProjectsStore()
  const settingsStore = useSettingsStore()
  const monthlyReportTemplatesStore = useMonthlyReportTemplatesStore()

  const selectedMonth = ref(new Date().getMonth() + 1)
  const selectedYear = ref(new Date().getFullYear())
  const selectedRate = ref<any>(null)
  const noRate = ref(false)

  const form = reactive<GenerateInvoiceDto>({
    customerId: 0,
    invoiceType: InvoiceType.Monthly,
    issueDate: localDateString(new Date()),
    workDays: [],
    expenses: [],
    monthlyReportTemplateId: undefined
  })

  const isMonthly = computed(() => form.invoiceType === InvoiceType.Monthly)

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
    workDays: toRef(form, 'workDays') as unknown as Ref<WorkDayDto[]>,
    year: selectedYear,
    month: selectedMonth,
    firstDayOfWeek: () => settingsStore.getFirstDayOfWeek(),
    defaultProjectName: () => defaultProjectName.value
  })
  const totals = workMonth.totals

  const isHourlyRate = computed(() => selectedRate.value?.type === RateType.Hourly)
  const isDailyRate = computed(() => selectedRate.value?.type === RateType.Daily)

  const customer = computed(() => customersStore.customers.find(c => c.id === form.customerId) ?? null)
  const sortedCustomers = computed(() => [...customersStore.customers].sort((a, b) => a.name.localeCompare(b.name)))

  const availableTemplates = computed(() => {
    if (!form.customerId) return []
    return monthlyReportTemplatesStore.templates.filter(
      t => t.customerId === form.customerId && invoiceTypeLabel(t.invoiceType) === 'Monthly'
    )
  })

  const activeTemplateName = computed(() => availableTemplates.value.find(t => t.isActive)?.name ?? '')

  const periodLabel = computed(() =>
    new Date(selectedYear.value, selectedMonth.value - 1, 1).toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
  )

  const rateUnit = computed(() => {
    const type = selectedRate.value?.type
    return type === RateType.Hourly ? 'per hour' : type === RateType.Monthly ? 'per month' : 'per day'
  })

  const estimatedAmount = computed(() => {
    if (!selectedRate.value || !isMonthly.value) return '-'

    const rate = selectedRate.value.price.amount
    const currency = selectedRate.value.price.currency

    if (selectedRate.value.type === RateType.Hourly) return formatMoney(totals.value.totalHours * rate, currency)
    if (selectedRate.value.type === RateType.Daily) return formatMoney(totals.value.effectiveDays * rate, currency)
    if (selectedRate.value.type === RateType.Monthly) return formatMoney(rate, currency)
    return '-'
  })

  // Sum per currency: an EUR invoice may carry a USD expense
  const expensesTotal = computed(() => {
    const byCurrency = new Map<string, number>()
    for (const e of form.expenses ?? []) {
      if (Number(e.amount) > 0) byCurrency.set(e.currency, (byCurrency.get(e.currency) ?? 0) + Number(e.amount))
    }
    return [...byCurrency.entries()].map(([currency, amount]) => formatMoney(amount, currency)).join(' + ')
  })

  function shiftMonth(offset: number) {
    const date = new Date(selectedYear.value, selectedMonth.value - 1 + offset, 1)
    if (!years.value.includes(date.getFullYear())) return
    selectedYear.value = date.getFullYear()
    selectedMonth.value = date.getMonth() + 1
  }

  // ---------- what each step still needs; null when it's complete ----------

  const customerBlocker = computed((): string | null => {
    if (!form.customerId) return 'Select a customer.'
    if (noRate.value) return 'The customer needs a rate.'
    if (!form.issueDate) return 'Set the issue date.'
    return null
  })

  const timeBlocker = computed((): string | null => {
    if (!isMonthly.value) return null
    if ((form.workDays ?? []).length === 0) return 'Mark at least one day.'
    // Hourly and daily billing need some hours on every worked day
    if (isHourlyRate.value || isDailyRate.value) {
      if (workMonth.workedDays.value.some(wd => (isHourlyRate.value ? dayHours(wd) : billedHours(wd)) <= 0)) {
        return 'Every worked day needs more than 0 hours.'
      }
    }
    return null
  })

  /** Index of the first expense missing a description or an amount, or -1. */
  const incompleteExpense = computed(() =>
    (form.expenses ?? []).findIndex(e => !e.description.trim() || !(Number(e.amount) > 0))
  )

  const expensesBlocker = computed((): string | null =>
    incompleteExpense.value >= 0 ? 'Complete or remove the unfinished expense.' : null
  )

  // ---------- data loading ----------

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
    // A timesheet template belongs to one customer
    form.monthlyReportTemplateId = undefined
    loadCustomerData()
  })

  async function initialize(preselectedCustomerId?: number) {
    resetMonth()
    await Promise.all([customersStore.fetchAll(), settingsStore.fetchInvoiceSettings()])
    if (preselectedCustomerId && customersStore.customers.some(c => c.id === preselectedCustomerId)) {
      form.customerId = preselectedCustomerId
    }
  }

  async function loadCustomerData() {
    selectedRate.value = null
    noRate.value = false
    if (!form.customerId) return

    try {
      await ratesStore.fetchByCustomerId(form.customerId)
      const rates = ratesStore.ratesByCustomer(form.customerId)

      if (rates.length === 0) {
        noRate.value = true
        return
      }

      // Same preference as the backend: Daily, then Monthly, then Hourly
      selectedRate.value =
        rates.find(r => r.type === RateType.Daily) ??
        rates.find(r => r.type === RateType.Monthly) ??
        rates.find(r => r.type === RateType.Hourly) ??
        rates[0]

      // The customer's projects, for suggestions on the calendar
      await projectsStore.fetchByCustomerId(form.customerId, false)

      if (isMonthly.value) {
        await monthlyReportTemplatesStore.fetchByCustomer(form.customerId)
      }
    } catch (error) {
      toast.failure('Could not load the customer’s rates and projects', error)
    }
  }

  // ---------- expenses ----------

  function addExpense() {
    form.expenses?.push({
      description: '',
      amount: 0,
      currency: selectedRate.value?.price?.currency ?? 'EUR'
    })
  }

  function removeExpense(index: number) {
    form.expenses?.splice(index, 1)
  }

  // ---------- payload ----------

  /** The request for the API: what's on screen, with incomplete project rows dropped. */
  function buildPayload(): GenerateInvoiceDto {
    const payload: GenerateInvoiceDto = {
      ...form,
      // Drop incomplete project rows; keep a day even if it ends up with no projects.
      // hoursWorked mirrors what the backend persists: the named projects' total when there are any,
      // otherwise the hours typed on unnamed rows (so partial days bill correctly without project names).
      workDays: isMonthly.value
        ? (form.workDays ?? []).map(wd => {
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
        : [],
      expenses: (form.expenses ?? []).map(e => ({ ...e, description: e.description.trim(), amount: Number(e.amount) }))
    }

    if (isMonthly.value) {
      payload.year = selectedYear.value
      payload.month = selectedMonth.value
    } else {
      payload.monthlyReportTemplateId = undefined
    }

    return payload
  }

  return {
    form,
    selectedMonth,
    selectedYear,
    selectedRate,
    noRate,
    isMonthly,
    isHourlyRate,
    isDailyRate,
    years,
    customer,
    sortedCustomers,
    projectSuggestions,
    availableTemplates,
    activeTemplateName,
    periodLabel,
    rateUnit,
    estimatedAmount,
    expensesTotal,
    workMonth,
    totals,
    customerBlocker,
    timeBlocker,
    expensesBlocker,
    incompleteExpense,
    shiftMonth,
    initialize,
    addExpense,
    removeExpense,
    buildPayload
  }
}

export type InvoiceDraft = ReturnType<typeof useInvoiceDraft>
