<template>
  <div class="tax-form">
    <form @submit.prevent="handleSubmit" class="form-content">
      <div class="form-group">
        <label for="description" class="form-label">
          Description <span class="text-red-500">*</span>
        </label>
        <input
          id="description"
          v-model="form.description"
          type="text"
          required
          placeholder="e.g., VAT, Sales Tax, Income Tax"
          class="form-control"
          :disabled="disabled"
        />
      </div>

      <div class="form-group">
        <label for="handlerId" class="form-label">
          Tax Calculation Type <span class="text-red-500">*</span>
        </label>
        <select
          id="handlerId"
          v-model="form.handlerId"
          required
          class="form-control"
          :disabled="disabled"
        >
          <option value="PERCENTAGE">Percentage Tax (e.g., 21% VAT)</option>
          <option value="FIXED_AMOUNT">Fixed Amount Tax</option>
          <option value="COMPOUND">Compound Tax (tax on another tax)</option>
        </select>
        <p class="form-hint">
          {{ getHandlerDescription(form.handlerId) }}
        </p>
      </div>

      <div class="form-group">
        <label for="rate" class="form-label">
          Rate (%) <span class="text-red-500">*</span>
        </label>
        <input
          id="rate"
          v-model.number="form.rate"
          type="number"
          step="0.01"
          min="0"
          max="100"
          required
          class="form-control"
          :class="{ 'border-red-500': errors.rate }"
          :disabled="disabled"
        />
        <p v-if="errors.rate" class="text-red-500 text-sm mt-1">{{ errors.rate }}</p>
      </div>

      <div class="form-group">
        <label for="order" class="form-label">
          Calculation Order <span class="text-red-500">*</span>
        </label>
        <input
          id="order"
          v-model.number="form.order"
          type="number"
          min="1"
          required
          class="form-control"
          :disabled="disabled"
        />
        <p class="form-hint">
          Taxes are calculated in ascending order. Use 1 for first, 2 for second, etc.
        </p>
      </div>

      <div v-if="form.handlerId === 'COMPOUND'" class="form-group">
        <label for="appliedToTaxId" class="form-label">
          Applied To Tax
        </label>
        <select
          id="appliedToTaxId"
          v-model="form.appliedToTaxId"
          class="form-control"
          :disabled="disabled"
        >
          <option :value="null">Select a tax (or leave empty for subtotal)</option>
          <option
            v-for="tax in availableTaxes"
            :key="tax.id"
            :value="tax.id"
          >
            {{ tax.description }} ({{ tax.rate }}%)
          </option>
        </select>
        <p class="form-hint">
          For compound tax: Select which tax this should be applied to, or leave empty to apply to invoice subtotal.
        </p>
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
          {{ loading ? 'Saving...' : 'Save Tax' }}
        </button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useTaxesStore } from '@/stores/taxes'
import { TaxApplicationType } from '@/types'
import type { CreateTaxDto, UpdateTaxDto } from '@/types'

interface Props {
  customerId: number
  taxId?: number
  disabled?: boolean
}

interface Emits {
  (e: 'success'): void
  (e: 'cancel'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()
const taxesStore = useTaxesStore()

const loading = ref(false)

const form = reactive<CreateTaxDto | UpdateTaxDto>({
  customerId: props.customerId,
  taxId: '',
  description: '',
  handlerId: 'PERCENTAGE',
  rate: 0,
  applicationType: TaxApplicationType.OnSubtotal,
  order: 1,
  appliedToTaxId: null
})

const errors = reactive<Record<string, string>>({})

const availableTaxes = computed(() => {
  return taxesStore.taxesByCustomer(props.customerId)
    .filter(t => props.taxId ? t.id !== props.taxId : true)
    .sort((a, b) => a.order - b.order)
})

onMounted(async () => {
  await taxesStore.fetchByCustomerId(props.customerId)
  if (props.taxId) {
    await loadTax(props.taxId)
  }
})

async function loadTax(id: number) {
  try {
    loading.value = true
    const tax = await taxesStore.fetchById(id)
    if (tax) {
      form.description = tax.description
      form.handlerId = tax.handlerId
      form.rate = tax.rate
      form.order = tax.order
      form.appliedToTaxId = tax.appliedToTaxId
    }
  } catch (error) {
    console.error('Failed to load tax:', error)
  } finally {
    loading.value = false
  }
}

function getHandlerDescription(handlerId: string): string {
  switch (handlerId) {
    case 'PERCENTAGE':
      return 'A percentage-based tax applied to the invoice subtotal (e.g., 21% VAT).'
    case 'FIXED_AMOUNT':
      return 'A fixed amount added to the invoice regardless of the subtotal.'
    case 'COMPOUND':
      return 'A tax calculated on top of another tax or the subtotal (e.g., provincial tax on federal tax).'
    default:
      return ''
  }
}

function validateForm(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])

  if (form.rate < 0 || form.rate > 100) {
    errors.rate = 'Rate must be between 0 and 100'
    return false
  }

  if (form.handlerId === 'COMPOUND' && form.appliedToTaxId) {
    const appliedToTax = taxesStore.getTaxById(form.appliedToTaxId)
    if (appliedToTax && appliedToTax.order >= form.order) {
      errors.rate = 'Compound tax must have a higher order than the tax it applies to'
      return false
    }
  }

  return true
}

async function handleSubmit() {
  if (!validateForm()) {
    return
  }

  try {
    loading.value = true

    if (props.taxId) {
      await taxesStore.update(props.taxId, form as UpdateTaxDto)
    } else {
      await taxesStore.create(form as CreateTaxDto)
    }

    emit('success')
  } catch (error: any) {
    console.error('Failed to save tax:', error)
    errors.rate = error.message || 'Failed to save tax'
  } finally {
    loading.value = false
  }
}

function handleCancel() {
  emit('cancel')
}
</script>

<style scoped>
.tax-form {
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

.form-hint {
  font-size: 0.75rem;
  color: #6b7280;
  margin-top: 0.25rem;
  font-style: italic;
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