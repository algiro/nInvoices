<template>
  <BasePanel :title="t('sampleData.title')" :description="t('sampleData.description')">
    <p class="note">{{ t('sampleData.note') }}</p>
    <div class="buttons">
      <BaseButton :loading="loading" @click="load">{{ t('sampleData.load') }}</BaseButton>
      <BaseButton variant="secondary" :loading="removing" @click="remove">{{ t('sampleData.remove') }}</BaseButton>
    </div>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { importExportApi } from '@/api/importExport'
import { customersApi } from '@/api/customers'
import { buildSampleData, SAMPLE_FISCAL_IDS } from '@/demo/sampleData'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

const { t } = useI18n()
const toast = useToast()
const { confirm } = useConfirm()

const loading = ref(false)
const removing = ref(false)

async function load() {
  loading.value = true
  try {
    const result = await importExportApi.importCustomers(buildSampleData(new Date()))
    if (result.errors.length > 0)
      toast.failure(t('sampleData.loadFailed'), new Error(result.errors.join('\n')))
    else if (result.imported === 0)
      toast.info(t('sampleData.alreadyThere'))
    else
      toast.success(t('sampleData.loaded'))
  } catch (error) {
    toast.failure(t('sampleData.loadFailed'), error)
  } finally {
    loading.value = false
  }
}

async function remove() {
  const ok = await confirm({
    title: t('sampleData.removeTitle'),
    message: t('sampleData.removeMessage'),
    confirmLabel: t('sampleData.remove'),
    tone: 'danger',
  })
  if (!ok) return

  removing.value = true
  try {
    const sample = (await customersApi.getAll()).filter(c => (SAMPLE_FISCAL_IDS as readonly string[]).includes(c.fiscalId))
    for (const customer of sample) await customersApi.delete(customer.id)
    toast.success(t('sampleData.removed', { count: sample.length }))
  } catch (error) {
    toast.failure(t('sampleData.removeFailed'), error)
  } finally {
    removing.value = false
  }
}
</script>

<style scoped>
.note {
  margin: 0 0 0.75rem;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
</style>
