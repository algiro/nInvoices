<template>
  <div class="bulk-bar" role="region" :aria-label="t('invoices.bulk.region')">
    <div class="bulk-info">
      <strong>{{ t('invoices.bulk.selected', { n: selected.size }) }}</strong>
      <button v-if="selection.canSelectAllMatching.value" type="button" class="link" :disabled="selection.selectingAll.value" @click="selection.selectAllMatching">
        {{ selection.selectingAll.value ? t('invoices.bulk.selecting') : t('invoices.bulk.selectAllMatching', { n: totalCount }) }}
      </button>
      <button type="button" class="link" @click="selected.clear()">{{ t('invoices.bulk.clearSelection') }}</button>
    </div>
    <div class="bulk-actions">
      <BaseButton size="sm" icon="lock" :disabled="!!bulkBusy || eligible('finalize') === 0" :loading="bulkBusy === 'finalize'" @click="runBulk('finalize')">
        {{ t('invoices.bulk.finalize.button', { n: eligible('finalize') }) }}
      </BaseButton>
      <BaseButton size="sm" icon="send" :disabled="!!bulkBusy || eligible('mark-as-sent') === 0" :loading="bulkBusy === 'mark-as-sent'" @click="runBulk('mark-as-sent')">
        {{ t('invoices.bulk.markAsSent.button', { n: eligible('mark-as-sent') }) }}
      </BaseButton>
      <BaseButton size="sm" icon="paid" :disabled="!!bulkBusy || eligible('mark-as-paid') === 0" :loading="bulkBusy === 'mark-as-paid'" @click="runBulk('mark-as-paid')">
        {{ t('invoices.bulk.markAsPaid.button', { n: eligible('mark-as-paid') }) }}
      </BaseButton>
      <span class="bulk-divider" aria-hidden="true"></span>
      <label class="check">
        <input v-model="includeTimesheets" type="checkbox" />
        {{ t('invoices.bulk.withTimesheets') }}
      </label>
      <BaseButton size="sm" variant="primary" icon="download" :disabled="!!bulkBusy" :loading="bulkBusy === 'download'" @click="downloadSelected">
        {{ t('invoices.bulk.downloadPdfs') }}
      </BaseButton>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { invoicesApi } from '@/api/invoices'
import type { BulkInvoiceChange } from '@/types'
import BaseButton from '@/components/ui/BaseButton.vue'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import type { InvoiceSelection } from '@/composables/useInvoiceSelection'
import { invoiceStatus } from '@/utils/format'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps<{
  selection: InvoiceSelection
  /** How many invoices match the filters. */
  totalCount: number
  /** Reloads the list after invoices changed status; the action stays busy until it is done. */
  refresh: () => Promise<unknown>
}>()

const toast = useToast()
const { confirm } = useConfirm()
const selected = props.selection.selected

// texts: invoices.bulk.<key>.*
const BULK_RULES: Record<BulkInvoiceChange, { statuses: string[]; key: 'finalize' | 'markAsSent' | 'markAsPaid' }> = {
  finalize: { statuses: ['Draft'], key: 'finalize' },
  'mark-as-sent': { statuses: ['Finalized'], key: 'markAsSent' },
  'mark-as-paid': { statuses: ['Finalized', 'Sent'], key: 'markAsPaid' }
}

/** How many selected invoices the change applies to (the server checks again). */
function eligible(change: BulkInvoiceChange): number {
  return [...selected.values()].filter(i => BULK_RULES[change].statuses.includes(invoiceStatus(i.status).name)).length
}

const bulkBusy = ref<BulkInvoiceChange | 'download' | null>(null)
const includeTimesheets = ref(false)

async function runBulk(change: BulkInvoiceChange) {
  const rule = BULK_RULES[change]
  const count = eligible(change)
  const others = selected.size - count
  const confirmed = await confirm({
    title: t(`invoices.bulk.${rule.key}.title`, count),
    message: others > 0 ? t(`invoices.bulk.${rule.key}.othersLeft`, others) : undefined,
    confirmLabel: t(`invoices.bulk.${rule.key}.verb`)
  })
  if (!confirmed) return

  bulkBusy.value = change
  try {
    const result = await invoicesApi.bulkChange(change, [...selected.keys()])
    if (result.succeeded.length) toast.success(t(`invoices.bulk.${rule.key}.done`, result.succeeded.length))
    if (result.skipped.length) {
      const examples = result.skipped.slice(0, 3).map(s => `${s.invoiceNumber ?? s.id}: ${s.reason}`).join('; ')
      toast.warning(t('invoices.bulk.leftUnchanged', result.skipped.length), {
        message: examples + (result.skipped.length > 3 ? '; …' : '')
      })
    }
    selected.clear()
    await props.refresh()
  } catch (error) {
    toast.failure(t(`invoices.bulk.${rule.key}.failed`), error)
  } finally {
    bulkBusy.value = null
  }
}

const MAX_DOWNLOAD = 100

async function downloadSelected() {
  if (selected.size > MAX_DOWNLOAD) {
    toast.warning(t('invoices.bulk.maxDownload', { max: MAX_DOWNLOAD }), { message: t('invoices.bulk.maxDownloadHelp') })
    return
  }

  bulkBusy.value = 'download'
  try {
    const { blob, fileName } = await invoicesApi.downloadZip([...selected.keys()], includeTimesheets.value)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName ?? `${t('nav.invoices')}.zip`
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
  } catch (error) {
    toast.failure(t('invoices.bulk.downloadFailed'), error)
  } finally {
    bulkBusy.value = null
  }
}
</script>

<style scoped>
/* stays in view while scrolling a long page */
.bulk-bar {
  position: sticky;
  top: 0.5rem;
  z-index: 15;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  margin-bottom: 0.75rem;
  padding: 0.6rem 0.9rem;
  border: 1px solid var(--color-primary-line);
  border-radius: var(--radius-lg);
  background: var(--color-primary-soft);
  box-shadow: var(--shadow-sm);
}

.bulk-info,
.bulk-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.75rem;
  font-size: var(--text-md);
}

.bulk-divider {
  width: 1px;
  height: 1.4rem;
  background: var(--color-border-strong);
}

.link {
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font-size: var(--text-sm);
  text-decoration: underline;
  cursor: pointer;
}

.check {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: var(--text-sm);
  color: var(--color-text-secondary);
}
</style>
