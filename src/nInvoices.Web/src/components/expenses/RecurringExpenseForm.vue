<template>
  <form class="expense-form" novalidate @submit.prevent="submit">
    <BaseField label="Description" for="recurring-description" required :error="errors.description">
      <input id="recurring-description" v-model="form.description" type="text" maxlength="500" class="control" placeholder="e.g. Social security fee (cuota de autónomo)" />
    </BaseField>

    <BaseField label="Category" for="recurring-category">
      <select id="recurring-category" v-model="form.category" class="control">
        <option v-for="(label, value) in ExpenseCategoryNames" :key="value" :value="value">{{ label }}</option>
      </select>
    </BaseField>

    <div class="row">
      <BaseField label="Amount each month" for="recurring-amount" required :error="errors.amount">
        <input id="recurring-amount" v-model.number="form.amount" type="number" step="0.01" min="0" inputmode="decimal" class="control num" />
      </BaseField>
      <BaseField label="Currency" for="recurring-currency" required>
        <select id="recurring-currency" v-model="form.currency" class="control">
          <option v-for="code in currencies" :key="code" :value="code">{{ code }}</option>
        </select>
      </BaseField>
    </div>

    <div class="row">
      <BaseField label="From" for="recurring-from" required :error="errors.fromMonth">
        <input id="recurring-from" v-model="form.fromMonth" type="month" class="control" />
      </BaseField>
      <BaseField label="Until" for="recurring-to" optional help="Leave empty while it goes on." :error="errors.toMonth">
        <input id="recurring-to" v-model="form.toMonth" type="month" class="control" />
      </BaseField>
    </div>

    <p class="hint">When the amount changes, end this one the month before and add a new one from the month it changes.</p>

    <p v-if="errors.form" class="form-error" role="alert">{{ errors.form }}</p>

    <div class="form-actions">
      <BaseButton :disabled="saving" @click="emit('cancel')">Cancel</BaseButton>
      <BaseButton type="submit" variant="primary" :loading="saving">{{ expense ? 'Save changes' : 'Add recurring expense' }}</BaseButton>
    </div>
  </form>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { businessExpensesApi } from '@/api'
import { ExpenseCategory, ExpenseCategoryNames } from '@/types'
import type { RecurringExpenseDto } from '@/types'
import { errorMessage, apiStatus, apiErrorData } from '@/composables/useToast'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import { fromMonthInput, toMonthInput, todayIso } from '@/utils/taxPrediction'

const props = defineProps<{
  /** The expense to edit; a new one when absent */
  expense?: RecurringExpenseDto | null
  defaultCurrency?: string
}>()

const emit = defineEmits<{ saved: [expense: RecurringExpenseDto]; cancel: [] }>()

const currencies = ['EUR', 'USD', 'GBP', 'CHF', 'JPY', 'CAD', 'AUD']
const saving = ref(false)
const errors = reactive<Record<string, string>>({})

const form = reactive({
  description: props.expense?.description ?? '',
  category: props.expense?.category ?? ExpenseCategory.SocialSecurity,
  amount: props.expense?.amount.amount ?? 0,
  currency: props.expense?.amount.currency ?? props.defaultCurrency ?? 'EUR',
  fromMonth: toMonthInput(props.expense?.fromMonth ?? todayIso()),
  toMonth: toMonthInput(props.expense?.toMonth)
})

function validate(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])
  if (!form.description.trim()) errors.description = 'Enter a description.'
  if (!(Number(form.amount) > 0)) errors.amount = 'Enter an amount greater than 0.'
  if (!fromMonthInput(form.fromMonth)) errors.fromMonth = 'Choose the first month.'
  if (form.toMonth && form.toMonth < form.fromMonth) errors.toMonth = 'It cannot end before it starts.'
  return Object.keys(errors).length === 0
}

async function submit() {
  if (!validate()) return

  const dto = {
    description: form.description.trim(),
    category: form.category,
    amount: { amount: Number(form.amount), currency: form.currency },
    fromMonth: fromMonthInput(form.fromMonth)!,
    toMonth: fromMonthInput(form.toMonth)
  }

  saving.value = true
  try {
    const saved = props.expense
      ? await businessExpensesApi.updateRecurring(props.expense.id, dto)
      : await businessExpensesApi.createRecurring(dto)
    emit('saved', saved)
  } catch (err) {
    const fieldErrors = apiStatus(err) === 400 ? (apiErrorData(err)?.errors as Record<string, string[]> | undefined) : undefined
    errors.form = fieldErrors ? Object.values(fieldErrors).flat().join(' ') : `The expense could not be saved: ${errorMessage(err)}`
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.expense-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr));
  gap: 0.75rem;
}

.hint {
  margin: 0;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.form-error {
  margin: 0;
  font-size: var(--text-sm);
  color: var(--color-danger);
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  padding-top: 0.25rem;
}
</style>
