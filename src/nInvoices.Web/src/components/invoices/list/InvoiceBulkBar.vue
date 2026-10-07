<template>
  <div class="bulk-bar" role="region" aria-label="Selected invoices">
    <div class="bulk-info">
      <strong>{{ selected.size }} selected</strong>
      <button v-if="selection.canSelectAllMatching.value" type="button" class="link" :disabled="selection.selectingAll.value" @click="selection.selectAllMatching">
        {{ selection.selectingAll.value ? 'Selecting…' : `Select all ${totalCount} matching` }}
      </button>
      <button type="button" class="link" @click="selected.clear()">Clear selection</button>
    </div>
    <div class="bulk-actions">
      <BaseButton size="sm" icon="lock" :disabled="!!bulkBusy || eligible('finalize') === 0" :loading="bulkBusy === 'finalize'" @click="runBulk('finalize')">
        Finalize ({{ eligible('finalize') }})
      </BaseButton>
      <BaseButton size="sm" icon="send" :disabled="!!bulkBusy || eligible('mark-as-sent') === 0" :loading="bulkBusy === 'mark-as-sent'" @click="runBulk('mark-as-sent')">
        Mark as sent ({{ eligible('mark-as-sent') }})
      </BaseButton>
      <BaseButton size="sm" icon="paid" :disabled="!!bulkBusy || eligible('mark-as-paid') === 0" :loading="bulkBusy === 'mark-as-paid'" @click="runBulk('mark-as-paid')">
        Mark as paid ({{ eligible('mark-as-paid') }})
      </BaseButton>
      <span class="bulk-divider" aria-hidden="true"></span>
      <label class="check">
        <input v-model="includeTimesheets" type="checkbox" />
        With timesheets
      </label>
      <BaseButton size="sm" variant="primary" icon="download" :disabled="!!bulkBusy" :loading="bulkBusy === 'download'" @click="downloadSelected">
        Download PDFs
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

const BULK_RULES: Record<BulkInvoiceChange, { statuses: string[]; verb: string; done: string }> = {
  finalize: { statuses: ['Draft'], verb: 'Finalize', done: 'finalized' },
  'mark-as-sent': { statuses: ['Finalized'], verb: 'Mark as sent', done: 'marked as sent' },
  'mark-as-paid': { statuses: ['Finalized', 'Sent'], verb: 'Mark as paid', done: 'marked as paid' }
}

/** How many selected invoices the change applies to (the server checks again). */
function eligible(change: BulkInvoiceChange): number {
  return [...selected.values()].filter(i => BULK_RULES[change].statuses.includes(invoiceStatus(i.status).name)).length
}

const bulkBusy = ref<BulkInvoiceChange | 'download' | null>(null)
const includeTimesheets = ref(false)

function plural(n: number) {
  return `${n} ${n === 1 ? 'invoice' : 'invoices'}`
}

async function runBulk(change: BulkInvoiceChange) {
  const rule = BULK_RULES[change]
  const count = eligible(change)
  const others = selected.size - count
  const confirmed = await confirm({
    title: `${rule.verb} ${plural(count)}?`,
    message: others > 0
      ? `${plural(others)} of the selection can't be ${rule.done} in ${others === 1 ? 'its' : 'their'} current status and will be left as ${others === 1 ? 'it is' : 'they are'}.`
      : undefined,
    confirmLabel: rule.verb
  })
  if (!confirmed) return

  bulkBusy.value = change
  try {
    const result = await invoicesApi.bulkChange(change, [...selected.keys()])
    if (result.succeeded.length) toast.success(`${plural(result.succeeded.length)} ${rule.done}`)
    if (result.skipped.length) {
      const examples = result.skipped.slice(0, 3).map(s => `${s.invoiceNumber ?? s.id}: ${s.reason}`).join('; ')
      toast.warning(`${plural(result.skipped.length)} left unchanged`, {
        message: examples + (result.skipped.length > 3 ? '; …' : '')
      })
    }
    selected.clear()
    await props.refresh()
  } catch (error) {
    toast.failure(`Could not ${rule.verb.toLowerCase()} the invoices`, error)
  } finally {
    bulkBusy.value = null
  }
}

const MAX_DOWNLOAD = 100

async function downloadSelected() {
  if (selected.size > MAX_DOWNLOAD) {
    toast.warning(`Select at most ${MAX_DOWNLOAD} invoices to download`, { message: 'The PDFs are rendered one by one; split larger downloads.' })
    return
  }

  bulkBusy.value = 'download'
  try {
    const { blob, fileName } = await invoicesApi.downloadZip([...selected.keys()], includeTimesheets.value)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName ?? 'Invoices.zip'
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
  } catch (error) {
    toast.failure('Could not download the PDFs', error)
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
