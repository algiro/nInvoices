<template>
  <LoadingState v-if="loadingTax" :label="t('taxes.loadingOne')" />

  <form v-else class="tax-form" novalidate @submit.prevent="handleSubmit">
    <BaseField :label="t('taxes.nameOnInvoice')" for="tax-description" required :error="errors.description">
      <input
        id="tax-description"
        v-model="form.description"
        type="text"
        :placeholder="t('taxes.namePlaceholder')"
        class="control"
        :disabled="disabled"
      />
    </BaseField>

    <BaseField :label="t('taxes.calculation')" for="tax-handler" required :help="handlerDescription(form.handlerId)">
      <select id="tax-handler" v-model="form.handlerId" class="control" :disabled="disabled">
        <option value="PERCENTAGE">{{ t('taxes.handlers.percentageOption') }}</option>
        <option value="FIXED_AMOUNT">{{ t('taxes.handlers.fixed') }}</option>
        <option value="COMPOUND">{{ t('taxes.handlers.compoundOption') }}</option>
      </select>
    </BaseField>

    <div class="row">
      <BaseField
        :label="form.handlerId === 'FIXED_AMOUNT' ? t('taxes.amount') : t('taxes.ratePercent')"
        for="tax-rate"
        required
        :error="errors.rate"
        :help="form.handlerId === 'FIXED_AMOUNT' ? undefined : t('taxes.rateHelp')"
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

      <BaseField :label="t('taxes.order')" for="tax-order" required :error="errors.order" :help="t('taxes.orderHelp')">
        <input id="tax-order" v-model.number="form.order" type="number" min="1" class="control num" :disabled="disabled" />
      </BaseField>
    </div>

    <BaseField
      v-if="form.handlerId === 'COMPOUND'"
      :label="t('taxes.calculatedOn')"
      for="tax-applied-to"
      :help="t('taxes.calculatedOnHelp')"
    >
      <select id="tax-applied-to" v-model="form.appliedToTaxId" class="control" :disabled="disabled">
        <option :value="null">{{ t('invoiceWizard.review.subtotal') }}</option>
        <option v-for="tax in availableTaxes" :key="tax.id" :value="tax.id">
          {{ tax.description }} ({{ tax.rate }}%)
        </option>
      </select>
    </BaseField>

    <label v-if="form.handlerId === 'PERCENTAGE'" class="toggle" for="tax-adds-to-base">
      <input id="tax-adds-to-base" v-model="form.addsToTaxableBase" type="checkbox" :disabled="disabled" />
      <span>
        <strong>{{ t('taxes.addsToBase') }}</strong>
        <small>{{ t('taxes.addsToBaseHelp') }}</small>
      </span>
    </label>

    <!-- What the countries the user turned on ask about a tax, e.g. which indirect tax it is, or why a 0% VAT charges nothing -->
    <template v-if="form.handlerId !== 'FIXED_AMOUNT'">
      <template v-for="country in complianceCountries" :key="country.countryCode">
        <ComplianceFieldInput
          v-for="field in visibleFields(country)"
          :key="`${country.countryCode}.${field.key}`"
          :field="field"
          :id="`tax-${country.countryCode}-${field.key}`"
          :value="complianceValue(country.countryCode, field.key)"
          @update="value => setComplianceValue(country.countryCode, field.key, value)"
        />
      </template>
    </template>

    <p v-if="errors.form" class="form-error" role="alert">{{ errors.form }}</p>

    <div class="form-actions">
      <BaseButton :disabled="saving" @click="emit('cancel')">{{ t('common.cancel') }}</BaseButton>
      <BaseButton type="submit" variant="primary" :loading="saving" :disabled="disabled">
        {{ taxId ? t('common.saveChanges') : t('taxes.add') }}
      </BaseButton>
    </div>
  </form>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useTaxesStore } from '@/stores/taxes'
import { TaxApplicationType } from '@/types'
import type { CreateTaxDto, UpdateTaxDto, ComplianceCountryDto } from '@/types'
import { complianceApi } from '@/api/compliance'
import ComplianceFieldInput from '@/components/settings/ComplianceFieldInput.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { errorMessage } from '@/composables/useToast'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

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
  appliedToTaxId: null,
  complianceValues: {},
  addsToTaxableBase: false
})

// The countries the user turned on that ask for extra data about a tax (none for most users)
const complianceCountries = ref<ComplianceCountryDto[]>([])

/** The fields that make sense for this tax: the reason for a zero rate is only asked when the rate is 0 */
function visibleFields(country: ComplianceCountryDto) {
  return country.taxFields.filter(field => !field.zeroRateOnly || form.rate === 0)
}

function complianceValue(country: string, key: string): string {
  return form.complianceValues?.[`${country}.${key}`] ?? ''
}

function setComplianceValue(country: string, key: string, value: string) {
  form.complianceValues = { ...form.complianceValues, [`${country}.${key}`]: value }
}

const errors = reactive<Record<string, string>>({})

const availableTaxes = computed(() => {
  return taxesStore.taxesByCustomer(props.customerId)
    .filter(t => props.taxId ? t.id !== props.taxId : true)
    .sort((a, b) => a.order - b.order)
})

onMounted(async () => {
  loadingTax.value = true
  complianceApi.getCountries()
    .then(countries => { complianceCountries.value = countries.filter(c => c.settings.isEnabled && c.taxFields.length > 0) })
    .catch(() => { /* without the list the tax is saved without country-specific data */ })
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
        form.addsToTaxableBase = tax.addsToTaxableBase
        form.complianceValues = { ...(tax.complianceValues ?? {}) }
      }
    } else {
      // a new tax goes after the existing ones
      form.order = Math.max(0, ...availableTaxes.value.map(t => t.order)) + 1
    }
  } catch (error) {
    errors.form = t('taxes.loadFailedDetail', { error: errorMessage(error) })
  } finally {
    loadingTax.value = false
  }
})

function handlerDescription(handlerId: string): string {
  switch (handlerId) {
    case 'PERCENTAGE':
      return t('taxes.handlers.percentageHelp')
    case 'FIXED_AMOUNT':
      return t('taxes.handlers.fixedHelp')
    case 'COMPOUND':
      return t('taxes.handlers.compoundHelp')
    default:
      return ''
  }
}

function validateForm(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])

  if (!form.description.trim()) {
    errors.description = t('taxes.enterName')
  }
  if (form.handlerId !== 'FIXED_AMOUNT' && (form.rate < -100 || form.rate > 100)) {
    errors.rate = t('taxes.enterRate')
  }
  if (!(form.order >= 1)) {
    errors.order = t('taxes.enterOrder')
  }
  if (form.handlerId === 'COMPOUND' && form.appliedToTaxId) {
    const appliedToTax = taxesStore.getTaxById(form.appliedToTaxId)
    if (appliedToTax && appliedToTax.order >= form.order) {
      errors.order = t('taxes.orderAfter', { name: appliedToTax.description, order: appliedToTax.order })
    }
  }

  const first = ['description', 'rate', 'order'].find(key => errors[key])
  if (first) document.getElementById(`tax-${first}`)?.focus()
  return !first
}

async function handleSubmit() {
  if (!validateForm()) return
  // Only a percentage of the subtotal is a contribution; the box is hidden for the others
  if (form.handlerId !== 'PERCENTAGE') form.addsToTaxableBase = false

  try {
    saving.value = true
    if (props.taxId) {
      await taxesStore.update(props.taxId, form as UpdateTaxDto)
    } else {
      await taxesStore.create(form as CreateTaxDto)
    }
    emit('success')
  } catch (error) {
    errors.form = t('taxes.saveFailedDetail', { error: errorMessage(error) })
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.toggle {
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
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
