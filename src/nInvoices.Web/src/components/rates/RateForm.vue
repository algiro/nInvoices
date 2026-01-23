<template>
  <div class="rate-form">
    <form @submit.prevent="handleSubmit" class="form-content">
      <div class="form-group">
        <label for="type" class="form-label">
          Rate Type <span class="text-red-500">*</span>
        </label>
        <select
          id="type"
          v-model="form.type"
          required
          class="form-control"
          :disabled="disabled"
        >
          <option value="Daily">Daily</option>
          <option value="Monthly">Monthly</option>
          <option value="Hourly">Hourly</option>
        </select>
      </div>

      <div class="form-group">
        <label for="amount" class="form-label">
          Amount <span class="text-red-500">*</span>
        </label>
        <input
          id="amount"
          v-model.number="form.price.amount"
          type="number"
          step="0.01"
          min="0"
          required
          class="form-control"
          :class="{ 'border-red-500': errors.amount }"
          :disabled="disabled"
        />
        <p v-if="errors.amount" class="text-red-500 text-sm mt-1">{{ errors.amount }}</p>
      </div>

      <div class="form-group">
        <label for="currency" class="form-label">
          Currency <span class="text-red-500">*</span>
        </label>
        <select
          id="currency"
          v-model="form.price.currency"
          required
          class="form-control"
          :disabled="disabled"
        >
          <option value="EUR">EUR - Euro</option>
          <option value="USD">USD - US Dollar</option>
          <option value="GBP">GBP - British Pound</option>
          <option value="CHF">CHF - Swiss Franc</option>
          <option value="JPY">JPY - Japanese Yen</option>
          <option value="CAD">CAD - Canadian Dollar</option>
          <option value="AUD">AUD - Australian Dollar</option>
        </select>
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
          :disabled="loading || disabled"
        >
          {{ loading ? 'Saving...' : 'Save Rate' }}
        </button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { reactive, onMounted } from 'vue'
import { useRatesStore } from '@/stores/rates'
import type { CreateRateDto, UpdateRateDto, RateType } from '@/types'

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

const loading = reactive({ value: false })

const form = reactive<CreateRateDto | UpdateRateDto>({
  customerId: props.customerId,
  type: 'Daily' as RateType,
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
    loading.value = true
    const rate = await ratesStore.fetchById(id)
    if (rate) {
      form.type = rate.type
      form.price = { ...rate.price }
    }
  } catch (error) {
    console.error('Failed to load rate:', error)
  } finally {
    loading.value = false
  }
}

function validateForm(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])

  if (form.price.amount <= 0) {
    errors.amount = 'Amount must be greater than 0'
    return false
  }

  if (!['EUR', 'USD', 'GBP', 'CHF', 'JPY', 'CAD', 'AUD'].includes(form.price.currency)) {
    errors.amount = 'Invalid currency'
    return false
  }

  return true
}

async function handleSubmit() {
  if (!validateForm()) {
    return
  }

  try {
    loading.value = true

    if (props.rateId) {
      await ratesStore.update(props.rateId, form as UpdateRateDto)
    } else {
      await ratesStore.create(form as CreateRateDto)
    }

    emit('success')
  } catch (error: any) {
    console.error('Failed to save rate:', error)
    errors.amount = error.message || 'Failed to save rate'
  } finally {
    loading.value = false
  }
}

function handleCancel() {
  emit('cancel')
}
</script>

<style scoped>
.rate-form {
  background: white;
  padding: 1.5rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.form-content {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.form-group {
  display: flex;
  flex-direction: column;
}

.form-label {
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
  transition: all 0.2s;
}

.form-control:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.form-control:disabled {
  background: #f3f4f6;
  cursor: not-allowed;
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

.btn-secondary:hover:not(:disabled) {
  background: #f9fafb;
}
</style>