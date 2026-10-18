<template>
  <BasePanel
    v-if="country || loadError"
    title="Tax regime"
    :description="`How you pay taxes in ${country?.name ?? 'your country'}, for the tax prediction. It only estimates: nothing is filed or paid for you.`"
  >
    <EmptyState v-if="loadError" icon="alert" title="The tax settings could not be loaded" :description="loadError" compact>
      <BaseButton @click="load">Try again</BaseButton>
    </EmptyState>

    <form v-else novalidate class="tax-form" @submit.prevent="save">
      <div class="grid">
        <BaseField v-if="country" label="Tax regime" for="tax-regime" required :help="regime?.help ?? undefined" :error="errors.regimeId">
          <select id="tax-regime" v-model="regimeId" class="control" @change="onRegimeChange">
            <option v-for="option in country.regimes" :key="option.id" :value="option.id">{{ option.name }}</option>
          </select>
        </BaseField>
      </div>

      <div v-if="regime" class="fields">
        <ComplianceFieldInput
          v-for="field in regime.fields"
          :id="`tax-${field.key}`"
          :key="field.key"
          :field="field"
          :value="values[field.key] ?? ''"
          :error="errors[field.key]"
          @update="value => (values[field.key] = value)"
        />
      </div>

      <p v-if="errors.general" class="field-error" role="alert">{{ errors.general }}</p>

      <div class="actions">
        <StatusPill v-if="!profile" tone="neutral">Not set up</StatusPill>
        <BaseButton type="submit" variant="primary" :loading="saving" :disabled="saving || !regime">Save</BaseButton>
      </div>
    </form>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted } from 'vue'
import { taxPredictionApi } from '@/api'
import type { TaxCountryDto, TaxProfileDto } from '@/types'
import { useToast, errorMessage, apiStatus, apiErrorData } from '@/composables/useToast'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import ComplianceFieldInput from '@/components/settings/ComplianceFieldInput.vue'
import { initialValues } from '@/utils/taxPrediction'

const props = defineProps<{
  /** The country the user works from: only its regimes are offered */
  countryCode: string
}>()

const toast = useToast()

const countries = ref<TaxCountryDto[]>([])
const profile = ref<TaxProfileDto | null>(null)
const loadError = ref<string | null>(null)
const saving = ref(false)

const countryCode = computed(() => props.countryCode)
const regimeId = ref('')
const values = ref<Record<string, string>>({})
// Field key (or countryCode / regimeId / general) -> message
const errors = ref<Record<string, string>>({})

const country = computed(() => countries.value.find(c => c.countryCode === countryCode.value) ?? null)
const regime = computed(() => country.value?.regimes.find(r => r.id === regimeId.value) ?? null)

function resetValues() {
  values.value = regime.value ? initialValues(regime.value, profile.value, countryCode.value) : {}
  errors.value = {}
}

function selectRegime() {
  const saved = profile.value?.countryCode === countryCode.value ? profile.value.regimeId : null
  regimeId.value = saved ?? country.value?.regimes[0]?.id ?? ''
  resetValues()
}

function onRegimeChange() {
  resetValues()
}

async function load() {
  loadError.value = null
  try {
    const settings = await taxPredictionApi.getSettings()
    countries.value = settings.countries
    profile.value = settings.profile ?? null
    selectRegime()
  } catch (err) {
    loadError.value = errorMessage(err, 'Failed to load the tax settings')
  }
}

async function save() {
  errors.value = {}
  saving.value = true
  try {
    profile.value = await taxPredictionApi.updateProfile({ countryCode: countryCode.value, regimeId: regimeId.value, values: values.value })
    values.value = { ...profile.value.values }
    toast.success('Tax settings saved')
  } catch (err) {
    if (apiStatus(err) === 400) {
      const byField: Record<string, string> = {}
      for (const issue of (apiErrorData(err)?.issues ?? []) as { field?: string | null; message: string }[]) {
        byField[issue.field ?? 'general'] = issue.message
      }
      errors.value = Object.keys(byField).length ? byField : { general: errorMessage(err) }
      // The messages sit under their fields, possibly out of sight: say so, and show the first one
      toast.warning('Tax settings not saved', { message: 'Check the fields marked in red.' })
      await nextTick()
      document.getElementById('tax-regime')?.closest('form')?.querySelector('[role="alert"]')
        ?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    } else {
      toast.failure('Failed to save the tax settings', err)
    }
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.tax-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
  gap: 0.75rem 1rem;
}

.fields {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.field-error {
  margin: 0;
  font-size: var(--text-sm);
  color: var(--color-danger);
}

.actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.75rem;
}
</style>
