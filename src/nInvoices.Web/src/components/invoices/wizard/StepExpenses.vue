<template>
  <BasePanel title="Expenses" description="Costs passed on to the customer, added after the worked time.">
    <template #actions>
      <BaseButton size="sm" icon="plus" @click="add">Add expense</BaseButton>
    </template>

    <p v-if="(form.expenses ?? []).length === 0" class="muted no-expenses">No expenses on this invoice. Add one, or continue to the review.</p>

    <div v-else class="expenses">
      <div v-for="(expense, index) in form.expenses" :key="index" class="expense-row">
        <input
          :id="`expense-description-${index}`"
          v-model="expense.description"
          type="text"
          placeholder="Description, e.g. Train to Milan"
          class="control"
          :class="{ invalid: showErrors && !expense.description.trim() }"
          :aria-label="`Expense ${index + 1} description`"
        />
        <input
          :id="`expense-amount-${index}`"
          v-model.number="expense.amount"
          type="number"
          step="0.01"
          min="0"
          class="control num amount"
          :class="{ invalid: showErrors && !(expense.amount > 0) }"
          :aria-label="`Expense ${index + 1} amount`"
        />
        <select :id="`expense-currency-${index}`" v-model="expense.currency" class="control currency" :aria-label="`Expense ${index + 1} currency`">
          <option v-for="code in EXPENSE_CURRENCIES" :key="code">{{ code }}</option>
        </select>
        <BaseButton variant="ghost-danger" icon="trash" icon-only :aria-label="`Remove expense ${index + 1}`" title="Remove" @click="removeExpense(index)" />
      </div>
    </div>
  </BasePanel>
</template>

<script setup lang="ts">
import { EXPENSE_CURRENCIES, type InvoiceDraft } from '@/composables/useInvoiceDraft'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

const props = defineProps<{
  draft: InvoiceDraft
  /** Mark incomplete rows, once the user has tried to move on. */
  showErrors: boolean
}>()

const { form, addExpense, removeExpense } = props.draft

function add() {
  addExpense()
  requestAnimationFrame(() => document.getElementById(`expense-description-${(form.expenses?.length ?? 1) - 1}`)?.focus())
}
</script>

<style scoped>
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
</style>
