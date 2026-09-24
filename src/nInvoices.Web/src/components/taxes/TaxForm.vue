<template>
  <LoadingState v-if="loadingTax" label="Loading tax…" />

  <form v-else class="tax-form" novalidate @submit.prevent="handleSubmit">
    <BaseField label="Name on the invoice" for="tax-description" required :error="errors.description">
      <input
        id="tax-description"
        v-model="form.description"
        type="text"
        placeholder="e.g. VAT, IRPF withholding"
        class="control"
        :disabled="disabled"
      />
    </BaseField>

    <BaseField label="Calculation" for="tax-handler" required :help="handlerDescription(form.handlerId)">
      <select id="tax-handler" v-model="form.handlerId" class="control" :disabled="disabled">
        <option value="PERCENTAGE">Percentage of the subtotal</option>
        <option value="FIXED_AMOUNT">Fixed amount</option>
        <option value="COMPOUND">Percentage of another tax</option>
      </select>
    </BaseField>

    <div class="row">
      <BaseField
        :label="form.handlerId === 'FIXED_AMOUNT' ? 'Amount' : 'Rate (%)'"
        for="tax-rate"
        required
        :error="errors.rate"
        :help="form.handlerId === 'FIXED_AMOUNT' ? undefined : 'Negative to subtract, e.g. −15 for withholding.'"
      >
        <input
          id="tax-rate"
          v-model.number="form.rate"
          type="number"
          step="0.01"
          min="-100"
          max="100"
          inputmode="decimal"
          class="control num"
          :disabled="disabled"
        />
      </BaseField>

      <BaseField label="Order" for="tax-order" required :error="errors.order" help="Lower numbers are calculated first.">
        <input id="tax-order" v-model.number="form.order" type="number" min="1" class="control num" :disabled="disabled" />
      </BaseField>
    </div>

    <BaseField
      v-if="form.handlerId === 'COMPOUND'"
      label="Calculated on"
      for="tax-applied-to"
      help="The tax this one is a percentage of. Leave on “Subtotal” to use the invoice subtotal."
    >
      <select id="tax-applied-to" v-model="form.appliedToTaxId" class="control" :disabled="disabled">
        <option :value="null">Subtotal</option>
        <option v-for="tax in availableTaxes" :key="tax.id" :value="tax.id">
          {{ tax.description }} ({{ tax.rate }}%)
        </option>
      </select>
    </BaseField>

    <p v-if="errors.form" class="form-error" role="alert">{{ errors.form }}</p>

    <div class="form-actions">
      <BaseButton :disabled="saving" @click="emit('cancel')">Cancel</BaseButton>
      <BaseButton type="submit" variant="primary" :loading="saving" :disabled="disabled">
        {{ taxId ? 'Save changes' : 'Add tax' }}
      </BaseButton>
    </div>
  </form>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useTaxesStore } from '@/stores/taxes'
import { TaxApplicationType } from '@/types'
import type { CreateTaxDto, UpdateTaxDto } from '@/types'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { errorMessage } from '@/composables/useToast'

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

const loadingTax = ref(false)
const saving = ref(false)

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
  loadingTax.value = true
  try {
    await taxesStore.fetchByCustomerId(props.customerId)
    if (props.taxId) {
      const tax = await taxesStore.fetchById(props.taxId)
      if (tax) {
        form.description = tax.description
        form.handlerId = tax.handlerId
        form.rate = tax.rate
        form.order = tax.order
        form.appliedToTaxId = tax.appliedToTaxId
      }
    } else {
      // a new tax goes after the existing ones
      form.order = Math.max(0, ...availableTaxes.value.map(t => t.order)) + 1
    }
  } catch (error) {
    errors.form = `The tax could not be loaded: ${errorMessage(error)}`
  } finally {
    loadingTax.value = false
  }
})

function handlerDescription(handlerId: string): string {
  switch (handlerId) {
    case 'PERCENTAGE':
      return 'A percentage of the invoice subtotal, e.g. 21% VAT.'
    case 'FIXED_AMOUNT':
      return 'The same amount on every invoice, whatever the subtotal.'
    case 'COMPOUND':
      return 'A percentage of another tax line, calculated after it.'
    default:
      return ''
  }
}

function validateForm(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])

  if (!form.description.trim()) {
    errors.description = 'Enter the name shown on the invoice.'
  }
  if (form.handlerId !== 'FIXED_AMOUNT' && (form.rate < -100 || form.rate > 100)) {
    errors.rate = 'Enter a rate between −100 and 100.'
  }
  if (!(form.order >= 1)) {
    errors.order = 'Enter 1 or higher.'
  }
  if (form.handlerId === 'COMPOUND' && form.appliedToTaxId) {
    const appliedToTax = taxesStore.getTaxById(form.appliedToTaxId)
    if (appliedToTax && appliedToTax.order >= form.order) {
      errors.order = `Must be higher than “${appliedToTax.description}” (order ${appliedToTax.order}), which it's calculated on.`
    }
  }

  const first = ['description', 'rate', 'order'].find(key => errors[key])
  if (first) document.getElementById(`tax-${first}`)?.focus()
  return !first
}

async function handleSubmit() {
  if (!validateForm()) return

  try {
    saving.value = true
    if (props.taxId) {
      await taxesStore.update(props.taxId, form as UpdateTaxDto)
    } else {
      await taxesStore.create(form as CreateTaxDto)
    }
    emit('success')
  } catch (error) {
    errors.form = `The tax could not be saved: ${errorMessage(error)}`
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.tax-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.row {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
}

@media (max-width: 520px) {
  .row {
    grid-template-columns: minmax(0, 1fr);
  }
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
