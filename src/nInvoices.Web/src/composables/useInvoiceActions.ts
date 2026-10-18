import { ref } from 'vue'
import { useInvoicesStore } from '@/stores/invoices'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import { invoiceStatus } from '@/utils/format'
import { t, type MessageKey } from '@/i18n'
import type { InvoiceDto } from '@/types'
import type { IconName } from '@/components/ui/icons'

export type InvoiceActionId =
  | 'finalize' | 'markSent' | 'markPaid' | 'cancel'
  | 'downloadPdf' | 'downloadReport' | 'regenerate' | 'verifyReport'
  | 'delete' | 'forceDelete'

export interface InvoiceAction {
  id: InvoiceActionId
  label: string
  icon: IconName
  danger?: boolean
}

const ACTION_DEFS: Record<InvoiceActionId, Omit<InvoiceAction, 'label'> & { label: MessageKey }> = {
  finalize: { id: 'finalize', label: 'invoices.actions.finalize', icon: 'lock' },
  markSent: { id: 'markSent', label: 'invoices.actions.markSent', icon: 'send' },
  markPaid: { id: 'markPaid', label: 'invoices.actions.markPaid', icon: 'paid' },
  cancel: { id: 'cancel', label: 'invoices.actions.cancel', icon: 'ban' },
  downloadPdf: { id: 'downloadPdf', label: 'invoices.actions.downloadPdf', icon: 'download' },
  downloadReport: { id: 'downloadReport', label: 'invoices.actions.downloadReport', icon: 'calendar' },
  regenerate: { id: 'regenerate', label: 'invoices.actions.regenerate', icon: 'refresh' },
  verifyReport: { id: 'verifyReport', label: 'invoices.actions.verifyReport', icon: 'check' },
  delete: { id: 'delete', label: 'invoices.actions.delete', icon: 'trash', danger: true },
  forceDelete: { id: 'forceDelete', label: 'invoices.actions.forceDelete', icon: 'trash', danger: true }
}

// Labels in the current language, read each time so they follow a language change
const ACTIONS = new Proxy({} as Record<InvoiceActionId, InvoiceAction>, {
  get: (_, id: string) => {
    const def = ACTION_DEFS[id as InvoiceActionId]
    return { ...def, label: t(def.label) }
  }
})

export function isMonthly(invoice: InvoiceDto): boolean {
  return String(invoice.type) === 'Monthly'
}

/**
 * The next step in the invoice's life (Draft → Finalized → Sent → Paid) and everything else
 * that applies to its current status. The UI shows `primary` as a button and `more` in a menu.
 */
export function actionsFor(invoice: InvoiceDto): { primary: InvoiceAction | null; more: (InvoiceAction | 'separator')[] } {
  const status = invoiceStatus(invoice.status).name
  const monthly = isMonthly(invoice)
  const documents: InvoiceAction[] = [
    ACTIONS.downloadPdf,
    ...(monthly ? [ACTIONS.downloadReport] : []),
    ACTIONS.regenerate,
    ...(monthly ? [ACTIONS.verifyReport] : [])
  ]

  switch (status) {
    case 'Draft':
      return { primary: ACTIONS.finalize, more: [...documents, 'separator', ACTIONS.delete] }
    case 'Finalized':
      return { primary: ACTIONS.markSent, more: [ACTIONS.markPaid, ...documents, 'separator', ACTIONS.cancel, ACTIONS.forceDelete] }
    case 'Sent':
      return { primary: ACTIONS.markPaid, more: [...documents, 'separator', ACTIONS.cancel, ACTIONS.forceDelete] }
    default: // Paid, Cancelled
      return { primary: null, more: [...documents, 'separator', ACTIONS.forceDelete] }
  }
}

/** Runs invoice actions with the right confirmation, feedback and error handling. */
export function useInvoiceActions() {
  const store = useInvoicesStore()
  const toast = useToast()
  const { confirm } = useConfirm()
  /** id of the invoice an action is running for, to show progress on its button */
  const busy = ref<{ id: number; action: InvoiceActionId } | null>(null)

  async function guarded(invoice: InvoiceDto, action: InvoiceActionId, work: () => Promise<void>, failure: string): Promise<boolean> {
    busy.value = { id: invoice.id, action }
    try {
      await work()
      return true
    } catch (error) {
      toast.failure(failure, error)
      return false
    } finally {
      busy.value = null
    }
  }

  /** Returns true when the action completed (false when cancelled or failed). */
  async function run(action: InvoiceActionId, invoice: InvoiceDto): Promise<boolean> {
    const number = invoice.invoiceNumber
    switch (action) {
      case 'finalize':
        if (!(await confirm({ title: t('invoices.actions.finalizeTitle', { number }), message: t('invoices.actions.finalizeMessage'), confirmLabel: t('invoices.actions.finalize') }))) return false
        return guarded(invoice, action, async () => {
          await store.finalize(invoice.id)
          toast.success(t('invoices.actions.finalized', { number }))
        }, t('invoices.actions.finalizeFailed'))

      case 'markSent':
        if (!(await confirm({ title: t('invoices.actions.markSentTitle', { number }), message: t('invoices.actions.markSentMessage'), confirmLabel: t('invoices.actions.markSent') }))) return false
        return guarded(invoice, action, async () => {
          await store.markAsSent(invoice.id)
          toast.success(t('invoices.actions.markedSent', { number }))
        }, t('invoices.actions.markSentFailed'))

      case 'markPaid':
        if (!(await confirm({ title: t('invoices.actions.markPaidTitle', { number }), message: t('invoices.actions.markPaidMessage'), confirmLabel: t('invoices.actions.markPaid') }))) return false
        return guarded(invoice, action, async () => {
          await store.markAsPaid(invoice.id)
          toast.success(t('invoices.actions.markedPaid', { number }))
        }, t('invoices.actions.markPaidFailed'))

      case 'cancel':
        if (!(await confirm({ title: t('invoices.actions.cancelTitle', { number }), message: t('invoices.actions.cancelMessage'), confirmLabel: t('invoices.actions.cancel'), cancelLabel: t('invoices.actions.keep'), tone: 'danger' }))) return false
        return guarded(invoice, action, async () => {
          await store.cancelInvoice(invoice.id)
          toast.success(t('invoices.actions.cancelled', { number }))
        }, t('invoices.actions.cancelFailed'))

      case 'downloadPdf':
        return guarded(invoice, action, () => store.downloadPdf(invoice.id, number), t('invoices.actions.downloadPdfFailed'))

      case 'downloadReport':
        return guarded(invoice, action, () => store.downloadMonthlyReportPdf(invoice.id, number), t('invoices.actions.downloadReportFailed'))

      case 'regenerate':
        if (!(await confirm({ title: t('invoices.actions.regenerateTitle', { number }), message: t('invoices.actions.regenerateMessage'), confirmLabel: t('invoices.actions.regenerateConfirm') }))) return false
        return guarded(invoice, action, async () => {
          const result = await store.regenerateInvoicePdf(invoice.id)
          toast.success(t('invoices.actions.regenerated'), { message: result?.message })
        }, t('invoices.actions.regenerateFailed'))

      case 'verifyReport':
        return guarded(invoice, action, async () => {
          const result = await store.regenerateMonthlyReportPdf(invoice.id)
          toast.success(t('invoices.actions.reportOk'), { message: result?.message })
        }, t('invoices.actions.reportProblem'))

      case 'delete':
        if (!(await confirm({ title: t('invoices.actions.deleteTitle', { number }), message: t('invoices.actions.deleteMessage'), confirmLabel: t('common.delete'), tone: 'danger' }))) return false
        return guarded(invoice, action, async () => {
          await store.remove(invoice.id, false)
          toast.success(t('invoices.actions.deleted', { number }))
        }, t('invoices.actions.deleteFailed'))

      case 'forceDelete':
        if (!(await confirm({
          title: t('invoices.actions.forceDeleteTitle', { number }),
          message: t('invoices.actions.forceDeleteMessage', { status: invoiceStatus(invoice.status).label }),
          confirmLabel: t('invoices.actions.forceDeleteConfirm'),
          tone: 'danger'
        }))) return false
        return guarded(invoice, action, async () => {
          await store.remove(invoice.id, true)
          toast.success(t('invoices.actions.deleted', { number }))
        }, t('invoices.actions.deleteFailed'))
    }
  }

  function isBusy(invoice: InvoiceDto, action?: InvoiceActionId): boolean {
    return busy.value?.id === invoice.id && (!action || busy.value.action === action)
  }

  return { run, busy, isBusy }
}
