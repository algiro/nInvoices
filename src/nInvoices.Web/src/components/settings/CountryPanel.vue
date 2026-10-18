<template>
  <BasePanel :title="t('country.title')" :description="t('country.description')">
    <LoadingState v-if="loading" :label="t('country.loading')" />

    <EmptyState v-else-if="loadError" icon="alert" :title="t('country.loadFailed')" :description="loadError" compact>
      <BaseButton @click="load">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="offered.length === 0"
      icon="info"
      :title="t('country.noneTitle')"
      :description="t('country.noneDescription')"
      compact
    />

    <form v-else class="country-form" novalidate @submit.prevent="save">
      <BaseField :label="t('address.country')" for="user-country" required>
        <select id="user-country" v-model="selected" class="control">
          <option value="" disabled>{{ t('common.select') }}</option>
          <option v-for="country in offered" :key="country.countryCode" :value="country.countryCode">{{ country.name }}</option>
        </select>
      </BaseField>

      <BaseButton type="submit" variant="primary" :loading="saving" :disabled="saving || !selected || (selected === current && isChosen)">
        {{ current && !isChosen && selected === current ? t('common.confirm') : t('common.save') }}
      </BaseButton>
    </form>

    <p v-if="current && !isChosen && !loading" class="hint">
      {{ t('country.inferred', { country: nameOf(current) }) }}
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
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

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
    loadError.value = errorMessage(err, t('country.loadFailed'))
  } finally {
    loading.value = false
  }
}

async function save() {
  const code = selected.value
  if (current.value && current.value !== code) {
    const ok = await confirm({
      title: t('country.switchTitle', { country: nameOf(code) }),
      message: t('country.switchMessage', { current: nameOf(current.value), country: nameOf(code) }),
      confirmLabel: t('country.switchTo', { country: nameOf(code) })
    })
    if (!ok) return
  }

  saving.value = true
  try {
    const result = await countryApi.set(code)
    current.value = result.countryCode ?? null
    isChosen.value = result.isChosen
    emit('country', current.value)
    toast.success(t('country.saved', { country: nameOf(code) }))
  } catch (err) {
    toast.failure(t('country.saveFailed'), err)
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
