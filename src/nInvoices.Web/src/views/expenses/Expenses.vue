<template>
  <div class="expenses-page">
    <PageHeader
      title="Expenses"
      subtitle="The costs of your business, deducted from your income in the tax prediction. Costs you bill to a customer belong on their invoice instead."
    >
      <template #actions>
        <BaseButton variant="primary" icon="plus" @click="openNew">
          {{ tab === 'recurring' ? 'New recurring expense' : 'New expense' }}
        </BaseButton>
      </template>
    </PageHeader>

    <BaseTabs v-model="tab" :tabs="tabs" label="Kinds of expense" />

    <BasePanel v-if="tab === 'one-off'" flush>
      <template #header>
        <div class="panel-head">
          <h2 class="panel-title">Expenses of</h2>
          <select v-model.number="year" class="control year" aria-label="Year">
            <option v-for="option in years" :key="option" :value="option">{{ option }}</option>
          </select>
        </div>
      </template>

      <LoadingState v-if="loading" label="Loading expenses…" />

      <EmptyState v-else-if="loadError" icon="alert" title="The expenses could not be loaded" :description="loadError" compact>
        <BaseButton @click="loadExpenses">Try again</BaseButton>
      </EmptyState>

      <EmptyState
        v-else-if="expenses.length === 0"
        icon="receipt"
        :title="`No expenses in ${year}`"
        description="Add what your business pays for: accountant, software, equipment, travel…"
        compact
      >
        <BaseButton variant="primary" icon="plus" @click="openNew">Add an expense</BaseButton>
      </EmptyState>

      <div v-else class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Date</th>
              <th scope="col">Description</th>
              <th scope="col">Category</th>
              <th scope="col" class="num">Amount</th>
              <th scope="col" class="num">IVA / IGIC</th>
              <th scope="col" class="num">Business use</th>
              <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="expense in expenses" :key="expense.id">
              <td>{{ formatDate(expense.date) }}</td>
              <td class="primary-cell">
                {{ expense.description }}
                <span v-if="expense.supplier" class="sub">{{ expense.supplier }}</span>
              </td>
              <td class="muted">{{ ExpenseCategoryNames[expense.category] }}</td>
              <td class="num primary-cell">{{ formatMoney(expense.amount.amount, expense.amount.currency) }}</td>
              <td class="num">
                <span v-if="expense.reverseCharge" class="muted" title="The supplier charged none: self-charged and deducted">reverse charge</span>
                <template v-else>{{ formatMoney(expense.indirectTax, expense.amount.currency) }}</template>
              </td>
              <td class="num">{{ expense.deductiblePercent }}%</td>
              <td class="actions">
                <BaseButton size="sm" variant="ghost" icon="edit" @click="openEdit(expense)">Edit</BaseButton>
                <BaseButton
                  size="sm"
                  variant="ghost-danger"
                  icon="trash"
                  icon-only
                  title="Delete"
                  :aria-label="`Delete ${expense.description}`"
                  @click="remove(expense)"
                />
              </td>
            </tr>
          </tbody>
          <tfoot>
            <tr v-for="total in totals" :key="total.currency">
              <th scope="row" colspan="3">Total {{ year }}</th>
              <td class="num primary-cell">{{ formatMoney(total.amount, total.currency) }}</td>
              <td class="num">{{ formatMoney(total.tax, total.currency) }}</td>
              <td colspan="2"></td>
            </tr>
          </tfoot>
        </table>
      </div>
    </BasePanel>

    <BasePanel
      v-else
      flush
      title="Paid every month"
      description="Counted in every month they run, without adding them one by one. The social security fee goes here."
    >
      <LoadingState v-if="loading" label="Loading recurring expenses…" />

      <EmptyState v-else-if="loadError" icon="alert" title="The recurring expenses could not be loaded" :description="loadError" compact>
        <BaseButton @click="loadRecurring">Try again</BaseButton>
      </EmptyState>

      <EmptyState
        v-else-if="recurring.length === 0"
        icon="refresh"
        title="No recurring expenses"
        description="Add the social security fee and anything else you pay every month."
        compact
      >
        <BaseButton variant="primary" icon="plus" @click="openNew">Add a recurring expense</BaseButton>
      </EmptyState>

      <div v-else class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Description</th>
              <th scope="col">Category</th>
              <th scope="col" class="num">Each month</th>
              <th scope="col">From</th>
              <th scope="col">Until</th>
              <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="expense in recurring" :key="expense.id">
              <td class="primary-cell">{{ expense.description }}</td>
              <td class="muted">{{ ExpenseCategoryNames[expense.category] }}</td>
              <td class="num primary-cell">{{ formatMoney(expense.amount.amount, expense.amount.currency) }}</td>
              <td>{{ formatMonth(expense.fromMonth) }}</td>
              <td>{{ expense.toMonth ? formatMonth(expense.toMonth) : 'ongoing' }}</td>
              <td class="actions">
                <BaseButton size="sm" variant="ghost" icon="edit" @click="openEdit(expense)">Edit</BaseButton>
                <BaseButton
                  size="sm"
                  variant="ghost-danger"
                  icon="trash"
                  icon-only
                  title="Delete"
                  :aria-label="`Delete ${expense.description}`"
                  @click="remove(expense)"
                />
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </BasePanel>

    <BaseDialog
      :open="dialog !== null"
      :title="dialogTitle"
      size="md"
      :close-on-overlay="false"
      @close="dialog = null"
    >
      <BusinessExpenseForm
        v-if="dialog?.kind === 'one-off'"
        :expense="dialog.expense"
        @saved="onSaved"
        @cancel="dialog = null"
      />
      <RecurringExpenseForm
        v-else-if="dialog?.kind === 'recurring'"
        :expense="dialog.expense"
        @saved="onSaved"
        @cancel="dialog = null"
      />
    </BaseDialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { businessExpensesApi } from '@/api'
import { ExpenseCategoryNames } from '@/types'
import type { BusinessExpenseDto, RecurringExpenseDto } from '@/types'
import { useToast, errorMessage } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseTabs from '@/components/ui/BaseTabs.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import BusinessExpenseForm from '@/components/expenses/BusinessExpenseForm.vue'
import RecurringExpenseForm from '@/components/expenses/RecurringExpenseForm.vue'
import { formatDate, formatMoney } from '@/utils/format'

type Dialog =
  | { kind: 'one-off'; expense: BusinessExpenseDto | null }
  | { kind: 'recurring'; expense: RecurringExpenseDto | null }

const toast = useToast()
const { confirm } = useConfirm()

const tab = ref('one-off')
const currentYear = new Date().getFullYear()
const year = ref(currentYear)
const years = Array.from({ length: 6 }, (_, i) => currentYear + 1 - i)

const expenses = ref<BusinessExpenseDto[]>([])
const recurring = ref<RecurringExpenseDto[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const dialog = ref<Dialog | null>(null)

const tabs = computed(() => [
  { id: 'one-off', label: 'One-off' },
  { id: 'recurring', label: 'Recurring', count: recurring.value.length || undefined }
])

const dialogTitle = computed(() => {
  if (!dialog.value) return ''
  const kind = dialog.value.kind === 'recurring' ? 'recurring expense' : 'expense'
  return dialog.value.expense ? `Edit ${kind}` : `New ${kind}`
})

/** Per currency: the amounts and the tax paid */
const totals = computed(() => {
  const byCurrency = new Map<string, { currency: string; amount: number; tax: number }>()
  for (const e of expenses.value) {
    const total = byCurrency.get(e.amount.currency) ?? { currency: e.amount.currency, amount: 0, tax: 0 }
    total.amount += e.amount.amount
    total.tax += e.indirectTax
    byCurrency.set(e.amount.currency, total)
  }
  return [...byCurrency.values()]
})

function formatMonth(date: string): string {
  return formatDate(date, { month: 'short', year: 'numeric' })
}

async function loadExpenses() {
  loading.value = true
  loadError.value = null
  try {
    expenses.value = await businessExpensesApi.getByYear(year.value)
  } catch (err) {
    loadError.value = errorMessage(err, 'Failed to load the expenses')
  } finally {
    loading.value = false
  }
}

async function loadRecurring() {
  loading.value = true
  loadError.value = null
  try {
    recurring.value = await businessExpensesApi.getRecurring()
  } catch (err) {
    loadError.value = errorMessage(err, 'Failed to load the recurring expenses')
  } finally {
    loading.value = false
  }
}

function openNew() {
  dialog.value = tab.value === 'recurring' ? { kind: 'recurring', expense: null } : { kind: 'one-off', expense: null }
}

function openEdit(expense: BusinessExpenseDto | RecurringExpenseDto) {
  dialog.value = 'fromMonth' in expense ? { kind: 'recurring', expense } : { kind: 'one-off', expense }
}

function onSaved() {
  const editing = !!dialog.value?.expense
  const recurringKind = dialog.value?.kind === 'recurring'
  dialog.value = null
  toast.success(editing ? 'Expense updated' : 'Expense added')
  if (recurringKind) loadRecurring()
  else loadExpenses()
}

async function remove(expense: BusinessExpenseDto | RecurringExpenseDto) {
  const isRecurring = 'fromMonth' in expense
  if (!(await confirm({
    title: 'Delete expense?',
    message: `“${expense.description}” will be deleted and no longer deducted in the tax prediction.`,
    confirmLabel: 'Delete',
    tone: 'danger'
  }))) {
    return
  }

  try {
    if (isRecurring) {
      await businessExpensesApi.deleteRecurring(expense.id)
      await loadRecurring()
    } else {
      await businessExpensesApi.delete(expense.id)
      await loadExpenses()
    }
    toast.success('Expense deleted')
  } catch (err) {
    toast.failure('Failed to delete the expense', err)
  }
}

watch(year, loadExpenses)
watch(tab, value => (value === 'recurring' ? loadRecurring() : loadExpenses()))

onMounted(async () => {
  await loadExpenses()
  // For the tab's count; errors show when the tab is opened
  businessExpensesApi.getRecurring().then(list => (recurring.value = list)).catch(() => {})
})
</script>

<style scoped>
.expenses-page {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 72rem;
}

.panel-head {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.panel-title {
  font-size: 1rem;
  font-weight: 600;
}

.year {
  width: auto;
}

tfoot th {
  text-align: left;
  font-weight: 600;
}

tfoot td,
tfoot th {
  padding: 0.6rem 0.9rem;
  border-top: 1px solid var(--color-border);
}
</style>
