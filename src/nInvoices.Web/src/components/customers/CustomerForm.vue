<template>
  <div class="customer-form">
    <PageHeader
      :title="isEditMode ? (loadedName ? `Edit ${loadedName}` : 'Edit customer') : 'New customer'"
      :subtitle="isEditMode ? 'Changes apply to invoices generated from now on.' : 'Who you bill, and where invoices are addressed.'"
    />

    <LoadingState v-if="loadingCustomer" label="Loading customer…" />

    <form v-else class="form" novalidate @submit.prevent="handleSubmit">
      <BasePanel title="Customer" description="Shown in the “Bill to” block of every invoice.">
        <div class="grid">
          <BaseField label="Name" for="customer-name" required :error="errors.name" class="span-2">
            <input id="customer-name" v-model="form.name" type="text" class="control" autocomplete="organization" />
          </BaseField>
          <BaseField label="VAT number / fiscal ID" for="customer-fiscal-id" required :error="errors.fiscalId">
            <input id="customer-fiscal-id" v-model="form.fiscalId" type="text" class="control" />
          </BaseField>
          <BaseField
            label="Document language"
            for="customer-locale"
            help="Used for dates and for month and day names in templates."
          >
            <select id="customer-locale" v-model="form.locale" class="control">
              <option v-for="option in localeOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
            </select>
          </BaseField>
        </div>
      </BasePanel>

      <BasePanel title="Billing address">
        <div class="grid address-grid">
          <BaseField label="Street" for="customer-street" required :error="errors.street" class="span-3">
            <input id="customer-street" v-model="form.address.street" type="text" class="control" autocomplete="address-line1" />
          </BaseField>
          <BaseField label="Number" for="customer-house-number" required :error="errors.houseNumber">
            <input id="customer-house-number" v-model="form.address.houseNumber" type="text" class="control" />
          </BaseField>
          <BaseField label="Postal code" for="customer-zip" required :error="errors.zipCode">
            <input id="customer-zip" v-model="form.address.zipCode" type="text" class="control" autocomplete="postal-code" />
          </BaseField>
          <BaseField label="City" for="customer-city" required :error="errors.city" class="span-3">
            <input id="customer-city" v-model="form.address.city" type="text" class="control" autocomplete="address-level2" />
          </BaseField>
          <BaseField label="State / province" for="customer-state" optional class="span-2">
            <input id="customer-state" v-model="stateValue" type="text" class="control" autocomplete="address-level1" />
          </BaseField>
          <BaseField label="Country" for="customer-country" required :error="errors.country" class="span-2">
            <input id="customer-country" v-model="form.address.country" type="text" class="control" autocomplete="country-name" />
          </BaseField>
        </div>
      </BasePanel>

      <BasePanel title="Invoice emails" description="Who receives the invoice when you create the email from the invoice page.">
        <div class="grid">
          <BaseField label="Email" for="customer-email" optional :error="errors.email">
            <input
              id="customer-email"
              v-model="emailValue"
              type="email"
              class="control"
              autocomplete="email"
              placeholder="accounts@example.com"
            />
          </BaseField>
          <BaseField label="CC" for="customer-cc" optional :error="errors.ccEmails" help="Several addresses separated by commas.">
            <input id="customer-cc" v-model="ccValue" type="text" class="control" placeholder="manager@example.com" />
          </BaseField>
        </div>
      </BasePanel>

      <div class="form-actions">
        <BaseButton :disabled="saving" @click="handleCancel">Cancel</BaseButton>
        <BaseButton type="submit" variant="primary" :loading="saving">
          {{ isEditMode ? 'Save changes' : 'Create customer' }}
        </BaseButton>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useCustomersStore } from '@/stores/customers'
import type { CreateCustomerDto, UpdateCustomerDto } from '@/types'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { useToast } from '@/composables/useToast'
import { setPageTitle } from '@/composables/usePageTitle'
import { localeLabel } from '@/utils/format'

interface Props {
  customerId?: number
}

const props = defineProps<Props>()
const router = useRouter()
const customersStore = useCustomersStore()
const toast = useToast()

const isEditMode = computed(() => !!props.customerId)
const loadingCustomer = ref(false)
const saving = ref(false)
const loadedName = ref('')

const form = reactive<CreateCustomerDto | UpdateCustomerDto>({
  name: '',
  fiscalId: '',
  locale: 'en-US',
  address: {
    street: '',
    houseNumber: '',
    city: '',
    zipCode: '',
    country: '',
    state: undefined
  }
})

// Empty email fields are sent as null; the inputs work with strings
const emailValue = computed({
  get: () => form.email ?? '',
  set: (value: string) => { form.email = value.trim() ? value.trim() : null }
})
const ccValue = computed({
  get: () => form.ccEmails ?? '',
  set: (value: string) => { form.ccEmails = value.trim() ? value : null }
})

const EMAIL_PATTERN = /^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$/

// An empty state is sent as absent; the input works with strings
const stateValue = computed({
  get: () => form.address.state ?? '',
  set: (value: string) => { form.address.state = value.trim() ? value : undefined }
})

const baseLocales = ['en-US', 'en-GB', 'it-IT', 'es-ES', 'de-DE', 'fr-FR', 'pt-PT', 'nl-NL']

const localeOptions = computed(() => {
  // keep a customer's existing locale selectable even if it isn't in the usual list
  const values = !form.locale || baseLocales.includes(form.locale) ? baseLocales : [form.locale, ...baseLocales]
  const options = values.map(value => ({ value, label: `${localeLabel(value)} (${value})` }))
  return form.locale ? options : [{ value: '', label: 'Not set, choose a language' }, ...options]
})

const errors = reactive<Record<string, string>>({})

onMounted(async () => {
  if (isEditMode.value && props.customerId) {
    await loadCustomer(props.customerId)
  }
})

async function loadCustomer(id: number) {
  try {
    loadingCustomer.value = true
    const customer = await customersStore.fetchById(id)
    if (customer) {
      form.name = customer.name
      form.fiscalId = customer.fiscalId
      form.locale = customer.locale ?? '' // keep an unset language unset until the user picks one
      form.address = { ...customer.address }
      form.email = customer.email ?? null
      form.ccEmails = customer.ccEmails ?? null
      loadedName.value = customer.name
      setPageTitle(`Edit ${customer.name}`)
    }
  } catch (error) {
    toast.failure('Could not load the customer', error)
  } finally {
    loadingCustomer.value = false
  }
}

function validateForm(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])
  const required: [keyof typeof errors | string, string, string][] = [
    ['name', form.name, 'Enter the customer’s name.'],
    ['fiscalId', form.fiscalId, 'Enter the VAT number or fiscal ID.'],
    ['street', form.address.street, 'Enter the street.'],
    ['houseNumber', form.address.houseNumber, 'Enter the number.'],
    ['zipCode', form.address.zipCode, 'Enter the postal code.'],
    ['city', form.address.city, 'Enter the city.'],
    ['country', form.address.country, 'Enter the country.']
  ]
  for (const [key, value, message] of required) {
    if (!String(value ?? '').trim()) errors[key as string] = message
  }
  if (form.email && !EMAIL_PATTERN.test(form.email)) errors.email = 'Enter an address like accounts@example.com.'
  const badCc = (form.ccEmails ?? '').split(/[,;\s]+/).filter(Boolean).find(a => !EMAIL_PATTERN.test(a))
  if (badCc) errors.ccEmails = `“${badCc}” is not a valid address.`
  const first = Object.keys(errors)[0]
  if (first) {
    const ids: Record<string, string> = {
      name: 'customer-name', fiscalId: 'customer-fiscal-id', street: 'customer-street', houseNumber: 'customer-house-number',
      zipCode: 'customer-zip', city: 'customer-city', country: 'customer-country',
      email: 'customer-email', ccEmails: 'customer-cc'
    }
    document.getElementById(ids[first])?.focus()
  }
  return !first
}

async function handleSubmit() {
  if (!validateForm()) return

  try {
    saving.value = true
    if (isEditMode.value && props.customerId) {
      await customersStore.update(props.customerId, form as UpdateCustomerDto)
      toast.success('Customer updated')
      router.push(`/customers/${props.customerId}`)
    } else {
      const created = await customersStore.create(form as CreateCustomerDto)
      toast.success('Customer created', { message: 'Next, add a rate so you can invoice them.' })
      router.push(created?.id ? { path: `/customers/${created.id}`, query: { tab: 'rates' } } : '/customers')
    }
  } catch (error) {
    toast.failure('Could not save the customer', error)
  } finally {
    saving.value = false
  }
}

function handleCancel() {
  router.push(isEditMode.value ? `/customers/${props.customerId}` : '/customers')
}
</script>

<style scoped>
.customer-form {
  max-width: 52rem;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem 1.1rem;
}

.address-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.span-2 { grid-column: span 2; }
.span-3 { grid-column: span 3; }

@media (max-width: 640px) {
  .grid,
  .address-grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .span-2,
  .span-3 {
    grid-column: auto;
  }
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}
</style>
