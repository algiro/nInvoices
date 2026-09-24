import { ref } from 'vue'
import { useInvoicesStore } from '@/stores/invoices'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import { invoiceStatus, invoiceTypeLabel } from '@/utils/format'
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

const ACTIONS: Record<InvoiceActionId, InvoiceAction> = {
  finalize: { id: 'finalize', label: 'Finalize', icon: 'lock' },
  markSent: { id: 'markSent', label: 'Mark as sent', icon: 'send' },
  markPaid: { id: 'markPaid', label: 'Mark as paid', icon: 'paid' },
  cancel: { id: 'cancel', label: 'Cancel invoice', icon: 'ban' },
  downloadPdf: { id: 'downloadPdf', label: 'Download PDF', icon: 'download' },
  downloadReport: { id: 'downloadReport', label: 'Download monthly report', icon: 'calendar' },
  regenerate: { id: 'regenerate', label: 'Regenerate PDF from template', icon: 'refresh' },
  verifyReport: { id: 'verifyReport', label: 'Check monthly report', icon: 'check' },
  delete: { id: 'delete', label: 'Delete draft', icon: 'trash', danger: true },
  forceDelete: { id: 'forceDelete', label: 'Force delete…', icon: 'trash', danger: true }
}

export function isMonthly(invoice: InvoiceDto): boolean {
  return invoiceTypeLabel(invoice.type) === 'Monthly'
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
        if (!(await confirm({ title: `Finalize ${number}?`, message: 'A finalized invoice is locked and can no longer be edited or deleted normally.', confirmLabel: 'Finalize' }))) return false
        return guarded(invoice, action, async () => {
          await store.finalize(invoice.id)
          toast.success(`${number} finalized`)
        }, 'Could not finalize the invoice')

      case 'markSent':
        if (!(await confirm({ title: `Mark ${number} as sent?`, message: 'Record that the invoice was sent to the customer.', confirmLabel: 'Mark as sent' }))) return false
        return guarded(invoice, action, async () => {
          await store.markAsSent(invoice.id)
          toast.success(`${number} marked as sent`)
        }, 'Could not mark the invoice as sent')

      case 'markPaid':
        if (!(await confirm({ title: `Mark ${number} as paid?`, message: 'Record that the customer has paid this invoice.', confirmLabel: 'Mark as paid' }))) return false
        return guarded(invoice, action, async () => {
          await store.markAsPaid(invoice.id)
          toast.success(`${number} marked as paid`)
        }, 'Could not mark the invoice as paid')

      case 'cancel':
        if (!(await confirm({ title: `Cancel ${number}?`, message: 'The invoice stays on record as cancelled.', confirmLabel: 'Cancel invoice', cancelLabel: 'Keep invoice', tone: 'danger' }))) return false
        return guarded(invoice, action, async () => {
          await store.cancelInvoice(invoice.id)
          toast.success(`${number} cancelled`)
        }, 'Could not cancel the invoice')

      case 'downloadPdf':
        return guarded(invoice, action, () => store.downloadPdf(invoice.id, number), 'Could not download the PDF')

      case 'downloadReport':
        return guarded(invoice, action, () => store.downloadMonthlyReportPdf(invoice.id, number), 'Could not download the monthly report')

      case 'regenerate':
        if (!(await confirm({ title: `Regenerate ${number}?`, message: 'The PDF is rebuilt with the customer’s current active invoice template. Amounts don’t change.', confirmLabel: 'Regenerate' }))) return false
        return guarded(invoice, action, async () => {
          const result = await store.regenerateInvoicePdf(invoice.id)
          toast.success('PDF regenerated', { message: result?.message })
        }, 'Could not regenerate the PDF')

      case 'verifyReport':
        return guarded(invoice, action, async () => {
          const result = await store.regenerateMonthlyReportPdf(invoice.id)
          toast.success('Monthly report renders correctly', { message: result?.message })
        }, 'The monthly report has a problem')

      case 'delete':
        if (!(await confirm({ title: `Delete ${number}?`, message: 'This draft will be permanently deleted.', confirmLabel: 'Delete', tone: 'danger' }))) return false
        return guarded(invoice, action, async () => {
          await store.remove(invoice.id, false)
          toast.success(`${number} deleted`)
        }, 'Could not delete the invoice')

      case 'forceDelete':
        if (!(await confirm({
          title: `Force delete ${number}?`,
          message: `This invoice is ${invoiceStatus(invoice.status).label.toLowerCase()} and normally can't be deleted. Only force delete to clean up a failed or mistaken invoice. This can't be undone.`,
          confirmLabel: 'Force delete',
          tone: 'danger'
        }))) return false
        return guarded(invoice, action, async () => {
          await store.remove(invoice.id, true)
          toast.success(`${number} deleted`)
        }, 'Could not delete the invoice')
    }
  }

  function isBusy(invoice: InvoiceDto, action?: InvoiceActionId): boolean {
    return busy.value?.id === invoice.id && (!action || busy.value.action === action)
  }

  return { run, busy, isBusy }
}
