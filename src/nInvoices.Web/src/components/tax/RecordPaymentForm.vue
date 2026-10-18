<template>
  <form class="payment-form" novalidate @submit.prevent="submit">
    <p class="intro">
      {{ t('tax.payment.intro', { form: form.name, period: period.name, year, estimate: formatMoney(form.estimated, currency) }) }}
    </p>

    <div class="row">
      <BaseField :label="t('tax.payment.amount')" for="payment-amount" required :help="t('tax.payment.amountHelp', { currency })" :error="errors.amount">
        <input id="payment-amount" v-model.number="amount" type="number" step="0.01" min="0" inputmode="decimal" class="control num" />
      </BaseField>
      <BaseField :label="t('tax.payment.paidOn')" for="payment-date" required :error="errors.paidOn">
        <input id="payment-date" v-model="paidOn" type="date" class="control" />
      </BaseField>
    </div>

    <p v-if="errors.form" class="form-error" role="alert">{{ errors.form }}</p>

    <div class="form-actions">
      <BaseButton v-if="form.payment" variant="ghost-danger" icon="trash" :loading="deleting" :disabled="saving" class="delete" @click="removePayment">
        {{ t('tax.payment.deleteRecord') }}
      </BaseButton>
      <BaseButton :disabled="saving || deleting" @click="emit('cancel')">{{ t('common.cancel') }}</BaseButton>
      <BaseButton type="submit" variant="primary" :loading="saving" :disabled="deleting">{{ t('common.save') }}</BaseButton>
    </div>
  </form>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { taxPredictionApi } from '@/api'
import type { TaxFormDto, TaxPeriodDto } from '@/types'
import { errorMessage } from '@/composables/useToast'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import { formatMoney } from '@/utils/format'
import { todayIso } from '@/utils/taxPrediction'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps<{
  form: TaxFormDto
  period: TaxPeriodDto
  year: number
  currency: string
}>()

const emit = defineEmits<{ saved: []; deleted: []; cancel: [] }>()

// A new record starts from the estimate, paid today or on the deadline if that has not come yet
const amount = ref<number>(props.form.payment?.amount ?? props.form.estimated)
const paidOn = ref(props.form.payment?.paidOn ?? [todayIso(), props.form.deadline].sort()[0])
const saving = ref(false)
const deleting = ref(false)
const errors = reactive<Record<string, string>>({})

async function submit() {
  Object.keys(errors).forEach(key => delete errors[key])
  if (!(Number(amount.value) >= 0) || amount.value === null || (amount.value as unknown) === '') errors.amount = t('tax.payment.enterAmount')
  if (!paidOn.value) errors.paidOn = t('expenses.form.enterDate')
  if (Object.keys(errors).length) return

  saving.value = true
  try {
    await taxPredictionApi.recordPayment({
      year: props.year,
      period: props.period.period,
      form: props.form.code,
      amount: Number(amount.value),
      paidOn: paidOn.value
    })
    emit('saved')
  } catch (err) {
    errors.form = t('tax.payment.saveFailedDetail', { error: errorMessage(err) })
  } finally {
    saving.value = false
  }
}

async function removePayment() {
  if (!props.form.payment) return
  deleting.value = true
  try {
    await taxPredictionApi.deletePayment(props.form.payment.id)
    emit('deleted')
  } catch (err) {
    errors.form = t('tax.payment.deleteFailedDetail', { error: errorMessage(err) })
  } finally {
    deleting.value = false
  }
}
</script>

<style scoped>
.payment-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.intro {
  margin: 0;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr));
  gap: 0.75rem;
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

.delete {
  margin-right: auto;
}
</style>
