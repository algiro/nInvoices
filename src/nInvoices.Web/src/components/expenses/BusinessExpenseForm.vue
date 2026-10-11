<template>
  <form class="expense-form" novalidate @submit.prevent="submit">
    <div class="row">
      <BaseField label="Date" for="expense-date" required help="The date on the supplier's invoice." :error="errors.date">
        <input id="expense-date" v-model="form.date" type="date" class="control" />
      </BaseField>
      <BaseField label="Category" for="expense-category">
        <select id="expense-category" v-model="form.category" class="control">
          <option v-for="(label, value) in ExpenseCategoryNames" :key="value" :value="value">{{ label }}</option>
        </select>
      </BaseField>
    </div>

    <BaseField label="Description" for="expense-description" required :error="errors.description">
      <input id="expense-description" v-model="form.description" type="text" maxlength="500" class="control" placeholder="e.g. Accountant, laptop, hosting" />
    </BaseField>

    <BaseField label="Supplier" for="expense-supplier" optional>
      <input id="expense-supplier" v-model="form.supplier" type="text" maxlength="200" class="control" />
    </BaseField>

    <div class="row">
      <BaseField label="Amount without tax" for="expense-amount" required :error="errors.amount">
        <input id="expense-amount" v-model.number="form.amount" type="number" step="0.01" min="0" inputmode="decimal" class="control num" />
      </BaseField>
      <BaseField label="Currency" for="expense-currency" required>
        <select id="expense-currency" v-model="form.currency" class="control">
          <option v-for="code in currencies" :key="code" :value="code">{{ code }}</option>
        </select>
      </BaseField>
    </div>

    <label class="toggle" for="expense-reverse-charge">
      <input id="expense-reverse-charge" v-model="form.reverseCharge" type="checkbox" />
      <span>
        <strong>The supplier charged no IVA/IGIC (reverse charge)</strong>
        <small>For a supplier abroad, such as a US software company: you charge the tax to yourself and deduct it.</small>
      </span>
    </label>

    <div class="row">
      <BaseField
        v-if="!form.reverseCharge"
        label="IVA / IGIC paid"
        for="expense-tax"
        help="As shown on the invoice; 0 if none."
        :error="errors.indirectTax"
      >
        <input id="expense-tax" v-model.number="form.indirectTax" type="number" step="0.01" min="0" inputmode="decimal" class="control num" />
      </BaseField>
      <BaseField label="Business use" for="expense-percent" help="The share that is a business cost, e.g. 30% for a home office." :error="errors.deductiblePercent">
        <div class="suffixed">
          <input id="expense-percent" v-model.number="form.deductiblePercent" type="number" step="1" min="0" max="100" class="control num" />
          <span>%</span>
        </div>
      </BaseField>
    </div>

    <p v-if="errors.form" class="form-error" role="alert">{{ errors.form }}</p>

    <div class="form-actions">
      <BaseButton :disabled="saving" @click="emit('cancel')">Cancel</BaseButton>
      <BaseButton type="submit" variant="primary" :loading="saving">{{ expense ? 'Save changes' : 'Add expense' }}</BaseButton>
    </div>
  </form>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { businessExpensesApi } from '@/api'
import { ExpenseCategory, ExpenseCategoryNames } from '@/types'
import type { BusinessExpenseDto } from '@/types'
import { errorMessage, apiStatus, apiErrorData } from '@/composables/useToast'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import { todayIso } from '@/utils/taxPrediction'

const props = defineProps<{
  /** The expense to edit; a new one when absent */
  expense?: BusinessExpenseDto | null
  /** For a new expense */
  defaultCurrency?: string
}>()

const emit = defineEmits<{ saved: [expense: BusinessExpenseDto]; cancel: [] }>()

const currencies = ['EUR', 'USD', 'GBP', 'CHF', 'JPY', 'CAD', 'AUD']
const saving = ref(false)
const errors = reactive<Record<string, string>>({})

const form = reactive({
  date: props.expense?.date ?? todayIso(),
  description: props.expense?.description ?? '',
  supplier: props.expense?.supplier ?? '',
  amount: props.expense?.amount.amount ?? 0,
  currency: props.expense?.amount.currency ?? props.defaultCurrency ?? 'EUR',
  indirectTax: props.expense?.indirectTax ?? 0,
  reverseCharge: props.expense?.reverseCharge ?? false,
  category: props.expense?.category ?? ExpenseCategory.Other,
  deductiblePercent: props.expense?.deductiblePercent ?? 100
})

function validate(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])
  if (!form.date) errors.date = 'Enter the date.'
  if (!form.description.trim()) errors.description = 'Enter a description.'
  if (!(Number(form.amount) > 0)) errors.amount = 'Enter an amount greater than 0.'
  if (!form.reverseCharge && Number(form.indirectTax) < 0) errors.indirectTax = 'The tax cannot be negative.'
  if (!(Number(form.deductiblePercent) >= 0 && Number(form.deductiblePercent) <= 100)) errors.deductiblePercent = 'Between 0 and 100.'
  return Object.keys(errors).length === 0
}

async function submit() {
  if (!validate()) return

  const dto = {
    date: form.date,
    description: form.description.trim(),
    supplier: form.supplier.trim() || null,
    amount: { amount: Number(form.amount), currency: form.currency },
    indirectTax: form.reverseCharge ? 0 : Number(form.indirectTax) || 0,
    reverseCharge: form.reverseCharge,
    category: form.category,
    deductiblePercent: Number(form.deductiblePercent)
  }

  saving.value = true
  try {
    const saved = props.expense
      ? await businessExpensesApi.update(props.expense.id, dto)
      : await businessExpensesApi.create(dto)
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

.suffixed {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.suffixed span {
  color: var(--color-text-muted);
}

.toggle {
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
  padding: 0.7rem 0.8rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  cursor: pointer;
}

.toggle input {
  width: 1rem;
  height: 1rem;
  margin-top: 0.15rem;
  accent-color: var(--color-primary);
}

.toggle span {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  font-size: var(--text-md);
}

.toggle small {
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
