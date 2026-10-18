<template>
  <LoadingState v-if="loadingRate" :label="t('rates.loadingOne')" />

  <form v-else class="rate-form" novalidate @submit.prevent="handleSubmit">
    <BaseField :label="t('customers.name')" for="rate-name" :help="t('rates.nameHelp')">
      <input id="rate-name" v-model="form.name" type="text" maxlength="100" class="control" :placeholder="t('rates.namePlaceholder')" :disabled="disabled" />
    </BaseField>

    <BaseField :label="t('rates.billed')" for="rate-type" required>
      <select id="rate-type" v-model.number="form.type" class="control" :disabled="disabled">
        <option :value="RateType.Daily">{{ t('rates.perDayOption') }}</option>
        <option :value="RateType.Hourly">{{ t('rates.perHourOption') }}</option>
        <option :value="RateType.Monthly">{{ t('rates.perMonthOption') }}</option>
      </select>
    </BaseField>

    <div class="price-row">
      <BaseField :label="t('rates.price')" for="rate-amount" required :error="errors.amount" class="amount">
        <input
          id="rate-amount"
          v-model.number="form.price.amount"
          type="number"
          step="0.01"
          min="0"
          inputmode="decimal"
          class="control num"
          :disabled="disabled"
        />
      </BaseField>
      <BaseField :label="t('common.currency')" for="rate-currency" required>
        <select id="rate-currency" v-model="form.price.currency" class="control" :disabled="disabled">
          <option v-for="code in currencies" :key="code" :value="code">{{ code }}</option>
        </select>
      </BaseField>
    </div>

    <p v-if="errors.form" class="form-error" role="alert">{{ errors.form }}</p>

    <div class="form-actions">
      <BaseButton :disabled="saving" @click="emit('cancel')">{{ t('common.cancel') }}</BaseButton>
      <BaseButton type="submit" variant="primary" :loading="saving" :disabled="disabled">
        {{ rateId ? t('common.saveChanges') : t('rates.add') }}
      </BaseButton>
    </div>
  </form>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRatesStore } from '@/stores/rates'
import { RateType } from '@/types'
import type { CreateRateDto, UpdateRateDto } from '@/types'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { errorMessage } from '@/composables/useToast'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface Props {
  customerId: number
  rateId?: number
  disabled?: boolean
}

interface Emits {
  (e: 'success'): void
  (e: 'cancel'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()
const ratesStore = useRatesStore()

const currencies = ['EUR', 'USD', 'GBP', 'CHF', 'JPY', 'CAD', 'AUD']
const loadingRate = ref(false)
const saving = ref(false)

const form = reactive<CreateRateDto | UpdateRateDto>({
  customerId: props.customerId,
  name: '',
  type: RateType.Daily,
  price: {
    amount: 0,
    currency: 'EUR'
  }
})

const errors = reactive<Record<string, string>>({})

onMounted(async () => {
  if (props.rateId) {
    await loadRate(props.rateId)
  }
})

async function loadRate(id: number) {
  try {
    loadingRate.value = true
    const rate = await ratesStore.fetchById(id)
    if (rate) {
      // The API sends the type's name ("Daily"); the form works with the enum value
      if (typeof rate.type === 'string') {
        const typeMap: Record<string, RateType> = { Daily: RateType.Daily, Monthly: RateType.Monthly, Hourly: RateType.Hourly }
        form.type = typeMap[rate.type] ?? RateType.Daily
      } else {
        form.type = rate.type
      }
      form.price = { ...rate.price }
      form.name = rate.name ?? ''
    }
  } catch (error) {
    errors.form = t('rates.loadFailedDetail', { error: errorMessage(error) })
  } finally {
    loadingRate.value = false
  }
}

function validateForm(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])
  if (!(Number(form.price.amount) > 0)) {
    errors.amount = t('rates.enterPrice')
    document.getElementById('rate-amount')?.focus()
  }
  return Object.keys(errors).length === 0
}

async function handleSubmit() {
  if (!validateForm()) return

  try {
    saving.value = true
    if (props.rateId) {
      const updateDto: UpdateRateDto = { type: form.type, price: form.price, name: form.name?.trim() || null }
      await ratesStore.update(props.rateId, updateDto)
    } else {
      await ratesStore.create({ ...(form as CreateRateDto), name: form.name?.trim() || null })
    }
    emit('success')
  } catch (error) {
    errors.form = t('rates.saveFailedDetail', { error: errorMessage(error) })
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.rate-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.price-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 7rem;
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
</style>
