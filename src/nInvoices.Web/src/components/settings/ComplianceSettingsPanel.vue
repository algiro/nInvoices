<template>
  <BasePanel
    v-if="countries.length || loadError"
    :title="t('compliance.title')"
    :description="t('compliance.description', { country: countries[0]?.name ?? t('taxProfile.yourCountry') })"
  >
    <EmptyState v-if="loadError" icon="alert" :title="t('compliance.loadFailed')" :description="loadError" compact>
      <BaseButton @click="load">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <div v-else class="countries">
      <section v-for="country in countries" :key="country.countryCode" class="country">
        <header class="country-header">
          <StatusPill :tone="country.settings.isEnabled ? 'success' : 'neutral'">
            {{ country.settings.isEnabled ? t('compliance.on') : t('holidays.off') }}
          </StatusPill>
        </header>

        <form v-if="forms[country.countryCode]" novalidate @submit.prevent="save(country)">
          <label class="toggle" :for="`compliance-${country.countryCode}-enabled`">
            <input :id="`compliance-${country.countryCode}-enabled`" v-model="forms[country.countryCode].isEnabled" type="checkbox" />
            <span>
              <strong>{{ t('compliance.apply', { country: country.name }) }}</strong>
              <small>{{ t('compliance.applyHelp') }}</small>
            </span>
          </label>

          <div class="grid">
            <BaseField :label="t('compliance.legalName')" :for="`${country.countryCode}-legalName`" :error="errors[country.countryCode]?.legalName">
              <input :id="`${country.countryCode}-legalName`" v-model="forms[country.countryCode].legalName" type="text" class="control" maxlength="200" />
            </BaseField>

            <BaseField :label="t('compliance.taxId')" :for="`${country.countryCode}-taxId`" :error="errors[country.countryCode]?.taxId">
              <input :id="`${country.countryCode}-taxId`" v-model="forms[country.countryCode].taxId" type="text" class="control mono" maxlength="50" />
            </BaseField>

            <ComplianceFieldInput
              v-for="field in country.fields"
              :key="field.key"
              :field="field"
              :id="`${country.countryCode}-${field.key}`"
              :value="forms[country.countryCode].values[field.key] ?? ''"
              :error="errors[country.countryCode]?.[field.key]"
              @update="value => forms[country.countryCode].values[field.key] = value"
            />
          </div>

          <fieldset class="address">
            <legend>{{ t('compliance.address') }}</legend>
            <div class="grid">
              <BaseField :label="t('address.street')" :for="`${country.countryCode}-street`" class="wide">
                <input :id="`${country.countryCode}-street`" v-model="forms[country.countryCode].address.street" type="text" class="control" maxlength="200" />
              </BaseField>
              <BaseField :label="t('address.number')" :for="`${country.countryCode}-houseNumber`">
                <input :id="`${country.countryCode}-houseNumber`" v-model="forms[country.countryCode].address.houseNumber" type="text" class="control" maxlength="20" />
              </BaseField>
              <BaseField :label="t('address.postalCode')" :for="`${country.countryCode}-zipCode`">
                <input :id="`${country.countryCode}-zipCode`" v-model="forms[country.countryCode].address.zipCode" type="text" class="control" maxlength="20" />
              </BaseField>
              <BaseField :label="t('address.city')" :for="`${country.countryCode}-city`">
                <input :id="`${country.countryCode}-city`" v-model="forms[country.countryCode].address.city" type="text" class="control" maxlength="100" />
              </BaseField>
              <BaseField :label="t('address.state')" :for="`${country.countryCode}-state`">
                <input :id="`${country.countryCode}-state`" v-model="forms[country.countryCode].address.state" type="text" class="control" maxlength="100" />
              </BaseField>
              <BaseField :label="t('address.country')" :for="`${country.countryCode}-country`">
                <input :id="`${country.countryCode}-country`" v-model="forms[country.countryCode].address.country" type="text" class="control" maxlength="100" />
              </BaseField>
            </div>
            <p v-if="errors[country.countryCode]?.address" class="field-error" role="alert">{{ errors[country.countryCode]?.address }}</p>
          </fieldset>

          <!-- What the country adds beyond its fields (countries/<code>), e.g. Spain's Verifactu record chain -->
          <component :is="countryUi(country.countryCode)?.complianceSettings" v-if="countryUi(country.countryCode)?.complianceSettings" :country="country" />

          <p v-if="errors[country.countryCode]?.general" class="field-error" role="alert">{{ errors[country.countryCode]?.general }}</p>

          <fieldset v-if="country.requiresCertificate" class="certificate">
            <legend>{{ t('compliance.certificate.title') }}</legend>
            <p class="cert-intro">
              {{ t('compliance.certificate.intro', { country: country.name }) }}
            </p>

            <div v-if="country.settings.certificate" class="cert-current">
              <StatusPill :tone="country.settings.certificate.isExpired ? 'danger' : 'success'">
                {{ country.settings.certificate.isExpired ? t('compliance.certificate.expired') : t('compliance.certificate.inUse') }}
              </StatusPill>
              <div class="cert-details">
                <strong class="mono">{{ country.settings.certificate.subject }}</strong>
                <span class="muted">{{ t('compliance.certificate.validUntil', { date: formatDate(country.settings.certificate.notAfter) }) }}</span>
              </div>
              <BaseButton variant="ghost-danger" :loading="certBusy === country.countryCode + ':remove'" :disabled="!!certBusy" @click="removeCertificate(country)">
                {{ t('common.remove') }}
              </BaseButton>
            </div>

            <div class="cert-upload">
              <BaseField :label="country.settings.certificate ? t('compliance.certificate.replaceWith') : t('compliance.certificate.file')" :for="`${country.countryCode}-cert-file`" :error="errors[country.countryCode]?.certificate">
                <input :id="`${country.countryCode}-cert-file`" type="file" accept=".p12,.pfx" class="control" @change="onCertificateFile(country.countryCode, $event)" />
              </BaseField>
              <BaseField :label="t('compliance.certificate.password')" :for="`${country.countryCode}-cert-password`">
                <input :id="`${country.countryCode}-cert-password`" v-model="certPasswords[country.countryCode]" type="password" class="control" autocomplete="off" />
              </BaseField>
              <BaseButton
                :loading="certBusy === country.countryCode + ':upload'"
                :disabled="!!certBusy || !certFiles[country.countryCode]"
                @click="uploadCertificate(country)"
              >
                {{ t('images.upload') }}
              </BaseButton>
            </div>
          </fieldset>

          <div class="actions">
            <BaseButton type="submit" variant="primary" :loading="saving === country.countryCode" :disabled="!!saving">{{ t('common.save') }}</BaseButton>
          </div>
        </form>
      </section>
    </div>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, reactive, nextTick, onMounted } from 'vue'
import { complianceApi } from '@/api'
import type { ComplianceCountryDto } from '@/types'
import { countryUi } from '@/countries'
import { useToast, errorMessage, apiStatus, apiErrorData } from '@/composables/useToast'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import ComplianceFieldInput from '@/components/settings/ComplianceFieldInput.vue'
import { formatDate } from '@/utils/format'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface CountryForm {
  isEnabled: boolean
  legalName: string
  taxId: string
  address: { street: string; houseNumber: string; city: string; zipCode: string; state: string; country: string }
  values: Record<string, string>
}

const props = defineProps<{
  /** The country the user works from: only its rules are shown */
  countryCode: string
}>()

const toast = useToast()

const countries = ref<ComplianceCountryDto[]>([])
const forms = reactive<Record<string, CountryForm>>({})
// Per country: field key (or 'address' / 'general') -> message
const errors = reactive<Record<string, Record<string, string>>>({})
const loadError = ref<string | null>(null)
const saving = ref<string | null>(null)

// Certificate upload: the chosen file (as base64) and password per country
const certFiles = reactive<Record<string, string | null>>({})
const certPasswords = reactive<Record<string, string>>({})
const certBusy = ref<string | null>(null)

async function onCertificateFile(code: string, event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  certFiles[code] = null
  if (!file) return
  const bytes = new Uint8Array(await file.arrayBuffer())
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  certFiles[code] = btoa(binary)
}

async function uploadCertificate(country: ComplianceCountryDto) {
  const code = country.countryCode
  const pfx = certFiles[code]
  if (!pfx) return
  errors[code] = { ...errors[code], certificate: '' }
  certBusy.value = `${code}:upload`
  try {
    const result = await complianceApi.setCertificate(code, pfx, certPasswords[code] ?? '')
    replaceKeepingForm(result)
    certFiles[code] = null
    certPasswords[code] = ''
    toast.success(t('compliance.certificate.stored'), { message: result.settings.certificate?.subject })
  } catch (err) {
    if (apiStatus(err) === 400) errors[code] = { ...errors[code], certificate: errorMessage(err, t('compliance.certificate.notAccepted')) }
    else toast.failure(t('compliance.certificate.storeFailed'), err)
  } finally {
    certBusy.value = null
  }
}

async function removeCertificate(country: ComplianceCountryDto) {
  const code = country.countryCode
  certBusy.value = `${code}:remove`
  try {
    replaceKeepingForm(await complianceApi.removeCertificate(code))
    toast.success(t('compliance.certificate.removed'))
  } catch (err) {
    toast.failure(t('compliance.certificate.removeFailed'), err)
  } finally {
    certBusy.value = null
  }
}

// Refreshes what the server owns (the certificate) without discarding what the user has typed in the form
function replaceKeepingForm(country: ComplianceCountryDto) {
  const index = countries.value.findIndex(c => c.countryCode === country.countryCode)
  if (index >= 0) countries.value[index] = country
}

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
    result.filter(c => c.countryCode === props.countryCode).forEach(apply)
  } catch (err) {
    loadError.value = errorMessage(err, t('compliance.loadFailed'))
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
    toast.success(result.settings.isEnabled ? t('compliance.turnedOn', { country: country.name }) : t('compliance.savedOff', { country: country.name }))
  } catch (err) {
    const data = apiErrorData(err)
    if (apiStatus(err) === 400) {
      const byField: Record<string, string> = {}
      for (const issue of (data?.issues ?? []) as { field?: string | null; message: string }[]) {
        const key = issue.field ?? 'general'
        byField[key] = byField[key] ? `${byField[key]} ${issue.message}` : issue.message
      }
      if (Object.keys(byField).length === 0) byField.address = errorMessage(err, t('compliance.notValid'))
      errors[code] = byField
      // The messages sit under their fields, possibly out of sight: say so, and show the first one
      toast.warning(t('compliance.notSaved', { country: country.name }), { message: t('common.checkFields') })
      await nextTick()
      document.getElementById(`${code}-legalName`)?.closest('form')?.querySelector('[role="alert"]')
        ?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    } else {
      toast.failure(t('compliance.saveFailed'), err)
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

.certificate {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 0.75rem 0.9rem 0.9rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.certificate legend {
  padding: 0 0.3rem;
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--color-text-secondary);
}

.cert-intro,
.muted {
  margin: 0;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.cert-current {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.cert-details {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
  overflow-wrap: anywhere;
}

.cert-upload {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
  gap: 0.75rem 1rem;
  align-items: end;
}
</style>
