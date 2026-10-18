<template>
  <BasePanel title="Your country" description="The country you work from as a freelancer. Only its invoicing rules and tax regimes apply to you.">
    <LoadingState v-if="loading" label="Loading countries…" />

    <EmptyState v-else-if="loadError" icon="alert" title="The countries could not be loaded" :description="loadError" compact>
      <BaseButton @click="load">Try again</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="offered.length === 0"
      icon="info"
      title="No country rules on this server"
      description="This installation offers no country's invoicing rules or tax estimates; invoices work without them."
      compact
    />

    <form v-else class="country-form" novalidate @submit.prevent="save">
      <BaseField label="Country" for="user-country" required>
        <select id="user-country" v-model="selected" class="control">
          <option value="" disabled>Select…</option>
          <option v-for="country in offered" :key="country.countryCode" :value="country.countryCode">{{ country.name }}</option>
        </select>
      </BaseField>

      <BaseButton type="submit" variant="primary" :loading="saving" :disabled="saving || !selected || (selected === current && isChosen)">
        {{ current && !isChosen && selected === current ? 'Confirm' : 'Save' }}
      </BaseButton>
    </form>

    <p v-if="current && !isChosen && !loading" class="hint">
      From your settings you work from {{ nameOf(current) }}. Confirm it, or choose another country.
    </p>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { countryApi } from '@/api'
import type { OfferedCountryDto } from '@/types'
import { useToast, errorMessage } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'

const emit = defineEmits<{
  /** The user's country, once known: on load, and after each change (null while there is none) */
  country: [countryCode: string | null]
}>()

const toast = useToast()
const { confirm } = useConfirm()

const offered = ref<OfferedCountryDto[]>([])
const current = ref<string | null>(null)
const isChosen = ref(false)
const selected = ref('')
const loading = ref(true)
const loadError = ref<string | null>(null)
const saving = ref(false)

function nameOf(code: string | null) {
  return offered.value.find(c => c.countryCode === code)?.name ?? code ?? ''
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await countryApi.get()
    offered.value = result.offered
    current.value = result.countryCode ?? null
    isChosen.value = result.isChosen
    selected.value = current.value ?? (result.offered.length === 1 ? result.offered[0].countryCode : '')
    emit('country', current.value)
  } catch (err) {
    loadError.value = errorMessage(err, 'Failed to load the countries')
  } finally {
    loading.value = false
  }
}

async function save() {
  const code = selected.value
  if (current.value && current.value !== code) {
    const ok = await confirm({
      title: `Work from ${nameOf(code)}?`,
      message: `${nameOf(current.value)}'s invoicing rules will be turned off (their settings are kept, for if you come back), and you choose your tax regime in ${nameOf(code)}.`,
      confirmLabel: `Switch to ${nameOf(code)}`
    })
    if (!ok) return
  }

  saving.value = true
  try {
    const result = await countryApi.set(code)
    current.value = result.countryCode ?? null
    isChosen.value = result.isChosen
    emit('country', current.value)
    toast.success(`You work from ${nameOf(code)}`)
  } catch (err) {
    toast.failure('Failed to save the country', err)
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.country-form {
  display: flex;
  align-items: flex-end;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.country-form :deep(.field) {
  flex: 1 1 16rem;
  max-width: 24rem;
}

.hint {
  margin: 0.75rem 0 0;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}
</style>
