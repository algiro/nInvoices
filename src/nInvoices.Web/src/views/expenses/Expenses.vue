<template>
  <div class="expenses-page">
    <PageHeader
      :title="t('nav.expenses')"
      :subtitle="t('expenses.subtitle')"
    >
      <template #actions>
        <BaseButton variant="primary" icon="plus" @click="openNew">
          {{ tab === 'recurring' ? t('expenses.newRecurring') : t('expenses.new') }}
        </BaseButton>
      </template>
    </PageHeader>

    <BaseTabs v-model="tab" :tabs="tabs" :label="t('expenses.kinds')" />

    <BasePanel v-if="tab === 'one-off'" flush>
      <template #header>
        <div class="panel-head">
          <h2 class="panel-title">{{ t('expenses.of') }}</h2>
          <select v-model.number="year" class="control year" :aria-label="t('common.year')">
            <option v-for="option in years" :key="option" :value="option">{{ option }}</option>
          </select>
        </div>
      </template>

      <LoadingState v-if="loading" :label="t('expenses.loading')" />

      <EmptyState v-else-if="loadError" icon="alert" :title="t('expenses.loadFailed')" :description="loadError" compact>
        <BaseButton @click="loadExpenses">{{ t('common.tryAgain') }}</BaseButton>
      </EmptyState>

      <EmptyState
        v-else-if="expenses.length === 0"
        icon="receipt"
        :title="t('expenses.noneIn', { year })"
        :description="t('expenses.emptyDescription')"
        compact
      >
        <BaseButton variant="primary" icon="plus" @click="openNew">{{ t('expenses.addOne') }}</BaseButton>
      </EmptyState>

      <div v-else class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">{{ t('expenses.date') }}</th>
              <th scope="col">{{ t('expenses.description') }}</th>
              <th scope="col">{{ t('expenses.category') }}</th>
              <th scope="col" class="num">{{ t('taxes.amount') }}</th>
              <th scope="col" class="num">{{ taxName }}</th>
              <th scope="col" class="num">{{ t('expenses.businessUse') }}</th>
              <th scope="col" class="actions"><span class="sr-only">{{ t('common.actions') }}</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="expense in expenses" :key="expense.id">
              <td>{{ formatDate(expense.date) }}</td>
              <td class="primary-cell">
                {{ expense.description }}
                <span v-if="expense.supplier" class="sub">{{ expense.supplier }}</span>
              </td>
              <td class="muted">{{ expenseCategoryLabel(expense.category) }}</td>
              <td class="num primary-cell">{{ formatMoney(expense.amount.amount, expense.amount.currency) }}</td>
              <td class="num">
                <span v-if="expense.reverseCharge" class="muted" :title="t('expenses.reverseChargeHint')">{{ t('expenses.reverseCharge') }}</span>
                <template v-else>{{ formatMoney(expense.indirectTax, expense.amount.currency) }}</template>
              </td>
              <td class="num">{{ expense.deductiblePercent }}%</td>
              <td class="actions">
                <BaseButton size="sm" variant="ghost" icon="edit" @click="openEdit(expense)">{{ t('common.edit') }}</BaseButton>
                <BaseButton
                  size="sm"
                  variant="ghost-danger"
                  icon="trash"
                  icon-only
                  :title="t('common.delete')"
                  :aria-label="t('common.deleteNamed', { name: expense.description })"
                  @click="remove(expense)"
                />
              </td>
            </tr>
          </tbody>
          <tfoot>
            <tr v-for="total in totals" :key="total.currency">
              <th scope="row" colspan="3">{{ t('expenses.totalYear', { year }) }}</th>
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
      :title="t('expenses.recurringTitle')"
      :description="t('expenses.recurringDescription')"
    >
      <LoadingState v-if="loading" :label="t('expenses.loadingRecurring')" />

      <EmptyState v-else-if="loadError" icon="alert" :title="t('expenses.loadRecurringFailed')" :description="loadError" compact>
        <BaseButton @click="loadRecurring">{{ t('common.tryAgain') }}</BaseButton>
      </EmptyState>

      <EmptyState
        v-else-if="recurring.length === 0"
        icon="refresh"
        :title="t('expenses.noRecurring')"
        :description="t('expenses.noRecurringDescription')"
        compact
      >
        <BaseButton variant="primary" icon="plus" @click="openNew">{{ t('expenses.addRecurring') }}</BaseButton>
      </EmptyState>

      <div v-else class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">{{ t('expenses.description') }}</th>
              <th scope="col">{{ t('expenses.category') }}</th>
              <th scope="col" class="num">{{ t('expenses.eachMonth') }}</th>
              <th scope="col">{{ t('expenses.from') }}</th>
              <th scope="col">{{ t('expenses.until') }}</th>
              <th scope="col" class="actions"><span class="sr-only">{{ t('common.actions') }}</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="expense in recurring" :key="expense.id">
              <td class="primary-cell">{{ expense.description }}</td>
              <td class="muted">{{ expenseCategoryLabel(expense.category) }}</td>
              <td class="num primary-cell">{{ formatMoney(expense.amount.amount, expense.amount.currency) }}</td>
              <td>{{ formatMonth(expense.fromMonth) }}</td>
              <td>{{ expense.toMonth ? formatMonth(expense.toMonth) : t('expenses.ongoing') }}</td>
              <td class="actions">
                <BaseButton size="sm" variant="ghost" icon="edit" @click="openEdit(expense)">{{ t('common.edit') }}</BaseButton>
                <BaseButton
                  size="sm"
                  variant="ghost-danger"
                  icon="trash"
                  icon-only
                  :title="t('common.delete')"
                  :aria-label="t('common.deleteNamed', { name: expense.description })"
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
        :tax-name="taxName"
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
import type { BusinessExpenseDto, RecurringExpenseDto } from '@/types'
import { useToast, errorMessage } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import { useIndirectTaxName } from '@/composables/useIndirectTaxName'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseTabs from '@/components/ui/BaseTabs.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import BusinessExpenseForm from '@/components/expenses/BusinessExpenseForm.vue'
import RecurringExpenseForm from '@/components/expenses/RecurringExpenseForm.vue'
import { expenseCategoryLabel, formatDate, formatMoney } from '@/utils/format'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

type Dialog =
  | { kind: 'one-off'; expense: BusinessExpenseDto | null }
  | { kind: 'recurring'; expense: RecurringExpenseDto | null }

const toast = useToast()
const taxName = useIndirectTaxName()
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
  { id: 'one-off', label: t('expenses.oneOff') },
  { id: 'recurring', label: t('expenses.recurring'), count: recurring.value.length || undefined }
])

const dialogTitle = computed(() => {
  if (!dialog.value) return ''
  if (dialog.value.kind === 'recurring') return dialog.value.expense ? t('expenses.editRecurring') : t('expenses.newRecurring')
  return dialog.value.expense ? t('expenses.edit') : t('expenses.new')
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
    loadError.value = errorMessage(err, t('expenses.loadFailed'))
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
    loadError.value = errorMessage(err, t('expenses.loadRecurringFailed'))
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
  toast.success(editing ? t('expenses.updated') : t('expenses.added'))
  if (recurringKind) loadRecurring()
  else loadExpenses()
}

async function remove(expense: BusinessExpenseDto | RecurringExpenseDto) {
  const isRecurring = 'fromMonth' in expense
  if (!(await confirm({
    title: t('expenses.deleteTitle'),
    message: t('expenses.deleteMessage', { name: expense.description }),
    confirmLabel: t('common.delete'),
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
    toast.success(t('expenses.deleted'))
  } catch (err) {
    toast.failure(t('expenses.deleteFailed'), err)
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
