<template>
  <BasePanel
    v-if="countries.length || loadError"
    title="Invoicing rules by country"
    description="Some countries require extra data or formats on invoices. Turn a country on only if its rules apply to you; with everything off, invoices work exactly as before."
  >
    <EmptyState v-if="loadError" icon="alert" title="The country rules could not be loaded" :description="loadError" compact>
      <BaseButton @click="load">Try again</BaseButton>
    </EmptyState>

    <div v-else class="countries">
      <section v-for="country in countries" :key="country.countryCode" class="country">
        <header class="country-header">
          <h3>{{ country.name }}</h3>
          <StatusPill :tone="country.settings.isEnabled ? 'success' : 'neutral'">
            {{ country.settings.isEnabled ? 'On' : 'Off' }}
          </StatusPill>
        </header>

        <form v-if="forms[country.countryCode]" novalidate @submit.prevent="save(country)">
          <label class="toggle" :for="`compliance-${country.countryCode}-enabled`">
            <input :id="`compliance-${country.countryCode}-enabled`" v-model="forms[country.countryCode].isEnabled" type="checkbox" />
            <span>
              <strong>Apply the {{ country.name }} rules to my invoices</strong>
              <small>Turning it on checks the details below.</small>
            </span>
          </label>

          <div class="grid">
            <BaseField label="Legal name" :for="`${country.countryCode}-legalName`" :error="errors[country.countryCode]?.legalName">
              <input :id="`${country.countryCode}-legalName`" v-model="forms[country.countryCode].legalName" type="text" class="control" maxlength="200" />
            </BaseField>

            <BaseField label="Tax ID" :for="`${country.countryCode}-taxId`" :error="errors[country.countryCode]?.taxId">
              <input :id="`${country.countryCode}-taxId`" v-model="forms[country.countryCode].taxId" type="text" class="control mono" maxlength="50" />
            </BaseField>

            <BaseField
              v-for="field in country.fields"
              :key="field.key"
              :label="field.label"
              :for="`${country.countryCode}-${field.key}`"
              :help="field.help ?? undefined"
              :error="errors[country.countryCode]?.[field.key]"
            >
              <select
                v-if="field.type === 'Choice'"
                :id="`${country.countryCode}-${field.key}`"
                v-model="forms[country.countryCode].values[field.key]"
                class="control"
              >
                <option value="">Select…</option>
                <option v-for="option in field.options ?? []" :key="option.value" :value="option.value">{{ option.label }}</option>
              </select>
              <input
                v-else-if="field.type === 'Boolean'"
                :id="`${country.countryCode}-${field.key}`"
                type="checkbox"
                :checked="forms[country.countryCode].values[field.key] === 'true'"
                @change="forms[country.countryCode].values[field.key] = ($event.target as HTMLInputElement).checked ? 'true' : ''"
              />
              <input
                v-else
                :id="`${country.countryCode}-${field.key}`"
                v-model="forms[country.countryCode].values[field.key]"
                type="text"
                class="control"
              />
            </BaseField>
          </div>

          <fieldset class="address">
            <legend>Address</legend>
            <div class="grid">
              <BaseField label="Street" :for="`${country.countryCode}-street`" class="wide">
                <input :id="`${country.countryCode}-street`" v-model="forms[country.countryCode].address.street" type="text" class="control" maxlength="200" />
              </BaseField>
              <BaseField label="Number" :for="`${country.countryCode}-houseNumber`">
                <input :id="`${country.countryCode}-houseNumber`" v-model="forms[country.countryCode].address.houseNumber" type="text" class="control" maxlength="20" />
              </BaseField>
              <BaseField label="Postal code" :for="`${country.countryCode}-zipCode`">
                <input :id="`${country.countryCode}-zipCode`" v-model="forms[country.countryCode].address.zipCode" type="text" class="control" maxlength="20" />
              </BaseField>
              <BaseField label="City" :for="`${country.countryCode}-city`">
                <input :id="`${country.countryCode}-city`" v-model="forms[country.countryCode].address.city" type="text" class="control" maxlength="100" />
              </BaseField>
              <BaseField label="Province / state" :for="`${country.countryCode}-state`" optional>
                <input :id="`${country.countryCode}-state`" v-model="forms[country.countryCode].address.state" type="text" class="control" maxlength="100" />
              </BaseField>
              <BaseField label="Country" :for="`${country.countryCode}-country`">
                <input :id="`${country.countryCode}-country`" v-model="forms[country.countryCode].address.country" type="text" class="control" maxlength="100" />
              </BaseField>
            </div>
            <p v-if="errors[country.countryCode]?.address" class="field-error" role="alert">{{ errors[country.countryCode]?.address }}</p>
          </fieldset>

          <p v-if="errors[country.countryCode]?.general" class="field-error" role="alert">{{ errors[country.countryCode]?.general }}</p>

          <div class="actions">
            <BaseButton type="submit" variant="primary" :loading="saving === country.countryCode" :disabled="!!saving">Save</BaseButton>
          </div>
        </form>
      </section>
    </div>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { complianceApi } from '@/api'
import type { ComplianceCountryDto } from '@/types'
import { useToast } from '@/composables/useToast'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'

interface CountryForm {
  isEnabled: boolean
  legalName: string
  taxId: string
  address: { street: string; houseNumber: string; city: string; zipCode: string; state: string; country: string }
  values: Record<string, string>
}

const toast = useToast()

const countries = ref<ComplianceCountryDto[]>([])
const forms = reactive<Record<string, CountryForm>>({})
// Per country: field key (or 'address' / 'general') -> message
const errors = reactive<Record<string, Record<string, string>>>({})
const loadError = ref<string | null>(null)
const saving = ref<string | null>(null)

function toForm(country: ComplianceCountryDto): CountryForm {
  const s = country.settings
  return {
    isEnabled: s.isEnabled,
    legalName: s.legalName ?? '',
    taxId: s.taxId ?? '',
    address: {
      street: s.address?.street ?? '',
      houseNumber: s.address?.houseNumber ?? '',
      city: s.address?.city ?? '',
      zipCode: s.address?.zipCode ?? '',
      state: s.address?.state ?? '',
      country: s.address?.country ?? ''
    },
    values: { ...s.values }
  }
}

function apply(country: ComplianceCountryDto) {
  const index = countries.value.findIndex(c => c.countryCode === country.countryCode)
  if (index >= 0) countries.value[index] = country
  else countries.value.push(country)
  forms[country.countryCode] = toForm(country)
  errors[country.countryCode] = {}
}

async function load() {
  loadError.value = null
  try {
    const result = await complianceApi.getCountries()
    countries.value = []
    result.forEach(apply)
  } catch (err: any) {
    loadError.value = err.message || 'Failed to load the country rules'
  }
}

async function save(country: ComplianceCountryDto) {
  const code = country.countryCode
  const form = forms[code]
  errors[code] = {}
  saving.value = code
  try {
    const result = await complianceApi.updateCountry(code, {
      isEnabled: form.isEnabled,
      legalName: form.legalName,
      taxId: form.taxId,
      address: { ...form.address, state: form.address.state || null },
      values: form.values
    })
    apply(result)
    toast.success(`${country.name} rules ${result.settings.isEnabled ? 'turned on' : 'saved (off)'}`)
  } catch (err: any) {
    const data = err.response?.data
    if (err.response?.status === 400) {
      const byField: Record<string, string> = {}
      for (const issue of (data?.issues ?? []) as { field?: string | null; message: string }[]) {
        const key = issue.field ?? 'general'
        byField[key] = byField[key] ? `${byField[key]} ${issue.message}` : issue.message
      }
      if (Object.keys(byField).length === 0) byField.address = data?.error || 'The settings are not valid'
      errors[code] = byField
    } else {
      toast.failure('Failed to save the country rules', err)
    }
  } finally {
    saving.value = null
  }
}

onMounted(load)
</script>

<style scoped>
.mono {
  font-family: var(--font-mono);
}

.countries {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.country-header {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin-bottom: 0.75rem;
}

.country-header h3 {
  margin: 0;
  font-size: var(--text-lg);
}

form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
  gap: 0.75rem 1rem;
}

.wide {
  grid-column: span 2;
}

@media (max-width: 560px) {
  .wide {
    grid-column: auto;
  }
}

.address {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 0.75rem 0.9rem 0.9rem;
}

.address legend {
  padding: 0 0.3rem;
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--color-text-secondary);
}

.field-error {
  margin: 0.5rem 0 0;
  font-size: var(--text-sm);
  color: var(--color-danger);
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

.actions {
  display: flex;
  justify-content: flex-end;
}
</style>
