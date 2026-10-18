<template>
  <div class="customer-form">
    <PageHeader
      :title="isEditMode ? (loadedName ? t('customers.form.editNamed', { name: loadedName }) : t('routes.editCustomer')) : t('customers.new')"
      :subtitle="isEditMode ? t('customers.form.editSubtitle') : t('customers.form.newSubtitle')"
    />

    <LoadingState v-if="loadingCustomer" :label="t('customers.loadingOne')" />

    <form v-else class="form" novalidate @submit.prevent="handleSubmit">
      <BasePanel :title="t('customers.customer')" :description="t('customers.form.customerDescription')">
        <div class="grid">
          <BaseField :label="t('customers.name')" for="customer-name" required :error="errors.name" class="span-2">
            <input id="customer-name" v-model="form.name" type="text" class="control" autocomplete="organization" />
          </BaseField>
          <BaseField :label="t('customers.fiscalId')" for="customer-fiscal-id" required :error="errors.fiscalId">
            <input id="customer-fiscal-id" v-model="form.fiscalId" type="text" class="control" />
          </BaseField>
          <BaseField
            :label="t('customers.documentLanguage')"
            for="customer-locale"
            :help="t('customers.form.documentLanguageHelp')"
          >
            <select id="customer-locale" v-model="form.locale" class="control">
              <option v-for="option in localeOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
            </select>
          </BaseField>
        </div>
      </BasePanel>

      <BasePanel :title="t('customers.billingAddress')">
        <div class="grid address-grid">
          <BaseField :label="t('address.street')" for="customer-street" required :error="errors.street" class="span-3">
            <input id="customer-street" v-model="form.address.street" type="text" class="control" autocomplete="address-line1" />
          </BaseField>
          <BaseField :label="t('address.number')" for="customer-house-number" required :error="errors.houseNumber">
            <input id="customer-house-number" v-model="form.address.houseNumber" type="text" class="control" />
          </BaseField>
          <BaseField :label="t('address.postalCode')" for="customer-zip" required :error="errors.zipCode">
            <input id="customer-zip" v-model="form.address.zipCode" type="text" class="control" autocomplete="postal-code" />
          </BaseField>
          <BaseField :label="t('address.city')" for="customer-city" required :error="errors.city" class="span-3">
            <input id="customer-city" v-model="form.address.city" type="text" class="control" autocomplete="address-level2" />
          </BaseField>
          <BaseField :label="t('address.state')" for="customer-state" optional class="span-2">
            <input id="customer-state" v-model="stateValue" type="text" class="control" autocomplete="address-level1" />
          </BaseField>
          <BaseField :label="t('address.country')" for="customer-country" required :error="errors.country" class="span-2">
            <input id="customer-country" v-model="form.address.country" type="text" class="control" autocomplete="country-name" />
          </BaseField>
        </div>
      </BasePanel>

      <BasePanel
        v-for="country in complianceCountries"
        :key="country.countryCode"
        :title="t('customers.form.countryInvoicing', { country: country.name })"
        :description="t('customers.form.countryInvoicingDescription')"
      >
        <div class="grid">
          <ComplianceFieldInput
            v-for="field in country.customerFields"
            :key="field.key"
            :field="field"
            :id="`customer-${country.countryCode}-${field.key}`"
            :value="complianceValue(country.countryCode, field.key)"
            :error="complianceErrors[field.key]"
            :class="{ 'span-2': field.type === 'Boolean' }"
            @update="value => setComplianceValue(country.countryCode, field.key, value)"
          />
        </div>
      </BasePanel>

      <BasePanel :title="t('customers.form.publicHolidays')" :description="t('customers.form.publicHolidaysDescription')">
        <div class="grid">
          <BaseField :label="t('customers.form.holidayCalendar')" for="customer-holiday-country" :help="holidayHelp">
            <select id="customer-holiday-country" v-model="holidayCountryValue" class="control">
              <option value="">{{ t('customers.form.addressCountry') }}</option>
              <option v-for="country in holidayCountries" :key="country.countryCode" :value="country.countryCode">
                {{ country.countryName }} ({{ country.countryCode }})
              </option>
            </select>
          </BaseField>
        </div>
      </BasePanel>

      <BasePanel :title="t('customers.form.invoiceEmails')" :description="t('customers.form.invoiceEmailsDescription')">
        <div class="grid">
          <BaseField :label="t('customers.form.email')" for="customer-email" optional :error="errors.email">
            <input
              id="customer-email"
              v-model="emailValue"
              type="email"
              class="control"
              autocomplete="email"
              placeholder="accounts@example.com"
            />
          </BaseField>
          <BaseField :label="t('customers.form.ccLabel')" for="customer-cc" optional :error="errors.ccEmails" :help="t('customers.form.ccHelp')">
            <input id="customer-cc" v-model="ccValue" type="text" class="control" placeholder="manager@example.com" />
          </BaseField>
        </div>
      </BasePanel>

      <div class="form-actions">
        <BaseButton :disabled="saving" @click="handleCancel">{{ t('common.cancel') }}</BaseButton>
        <BaseButton type="submit" variant="primary" :loading="saving">
          {{ isEditMode ? t('common.saveChanges') : t('customers.form.create') }}
        </BaseButton>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useCustomersStore } from '@/stores/customers'
import type { CreateCustomerDto, UpdateCustomerDto, HolidayCountryDto, ComplianceCountryDto } from '@/types'
import { holidaysApi } from '@/api/holidays'
import { complianceApi } from '@/api/compliance'
import ComplianceFieldInput from '@/components/settings/ComplianceFieldInput.vue'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { useToast, apiErrorData, apiStatus } from '@/composables/useToast'
import { setPageTitle } from '@/composables/usePageTitle'
import { localeLabel } from '@/utils/format'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

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
  },
  complianceValues: {}
})

// The countries the user turned on that ask for extra data on each customer (none for most users)
const complianceCountries = ref<ComplianceCountryDto[]>([])
const complianceErrors = reactive<Record<string, string>>({})

function complianceValue(country: string, key: string): string {
  return form.complianceValues?.[`${country}.${key}`] ?? ''
}

function setComplianceValue(country: string, key: string, value: string) {
  form.complianceValues = { ...form.complianceValues, [`${country}.${key}`]: value }
}

// Empty email fields are sent as null; the inputs work with strings
const emailValue = computed({
  get: () => form.email ?? '',
  set: (value: string) => { form.email = value.trim() ? value.trim() : null }
})
// "" means "follow the address" and is sent as null
const holidayCountryValue = computed({
  get: () => form.holidayCountry ?? '',
  set: (value: string) => { form.holidayCountry = value || null }
})

const holidayCountries = ref<HolidayCountryDto[]>([])
// The country the saved customer's address resolves to, to explain the default
const addressHolidayCountry = ref<string | null>(null)

const holidayHelp = computed(() => {
  if (form.holidayCountry) return t('customers.form.holidaysEditedIn')
  if (addressHolidayCountry.value) return t('customers.form.addressResolvesTo', { country: addressHolidayCountry.value })
  return t('customers.form.addressMatched')
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
  return form.locale ? options : [{ value: '', label: t('customers.form.localeNotSet') }, ...options]
})

const errors = reactive<Record<string, string>>({})

onMounted(async () => {
  holidaysApi.getCountries()
    .then(countries => { holidayCountries.value = countries })
    .catch(() => { /* the list is a convenience: without it only "address country" is offered */ })
  complianceApi.getCountries()
    .then(countries => { complianceCountries.value = countries.filter(c => c.settings.isEnabled && c.customerFields.length > 0) })
    .catch(() => { /* without the list the customer is saved without country-specific data */ })
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
      form.holidayCountry = customer.holidayCountry ?? null
      form.complianceValues = { ...(customer.complianceValues ?? {}) }
      addressHolidayCountry.value = customer.holidayCountry ? null : customer.effectiveHolidayCountry ?? null
      loadedName.value = customer.name
      setPageTitle(t('customers.form.editNamed', { name: customer.name }))
    }
  } catch (error) {
    toast.failure(t('customers.loadOneFailed'), error)
  } finally {
    loadingCustomer.value = false
  }
}

function validateForm(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])
  const required: [keyof typeof errors | string, string, string][] = [
    ['name', form.name, t('customers.form.enterName')],
    ['fiscalId', form.fiscalId, t('customers.form.enterFiscalId')],
    ['street', form.address.street, t('address.enterStreet')],
    ['houseNumber', form.address.houseNumber, t('address.enterNumber')],
    ['zipCode', form.address.zipCode, t('address.enterPostalCode')],
    ['city', form.address.city, t('address.enterCity')],
    ['country', form.address.country, t('address.enterCountry')]
  ]
  for (const [key, value, message] of required) {
    if (!String(value ?? '').trim()) errors[key as string] = message
  }
  if (form.email && !EMAIL_PATTERN.test(form.email)) errors.email = t('customers.form.enterEmail')
  const badCc = (form.ccEmails ?? '').split(/[,;\s]+/).filter(Boolean).find(a => !EMAIL_PATTERN.test(a))
  if (badCc) errors.ccEmails = t('customers.form.invalidAddress', { address: badCc })
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
      toast.success(t('customers.updated'))
      router.push(`/customers/${props.customerId}`)
    } else {
      const created = await customersStore.create(form as CreateCustomerDto)
      toast.success(t('customers.created'), { message: t('customers.createdNext') })
      router.push(created?.id ? { path: `/customers/${created.id}`, query: { tab: 'rates' } } : '/customers')
    }
  } catch (error) {
    const issues = apiErrorData(error)?.issues as { field?: string | null; message: string }[] | undefined
    Object.keys(complianceErrors).forEach(key => delete complianceErrors[key])
    if (apiStatus(error) === 400 && issues?.length) {
      for (const issue of issues) complianceErrors[issue.field ?? 'general'] = issue.message
      toast.failure(t('customers.form.invoicingDetailsMissing'), error)
    } else {
      toast.failure(t('customers.saveFailed'), error)
    }
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
