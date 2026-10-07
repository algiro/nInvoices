// @vitest-environment jsdom
import { describe, it, expect } from 'vitest'
import type { InvoiceDto } from '@/types'
import { actionsFor } from './useInvoiceActions'

function invoice(status: string, type = 'Monthly'): InvoiceDto {
  const money = { amount: 0, currency: 'EUR' }
  return {
    id: 1, customerId: 1, type, invoiceNumber: '26-09-001', issueDate: '2026-09-30',
    subtotal: money, totalExpenses: money, totalTaxes: money, total: money, status, createdAt: '2026-09-30'
  }
}

const ids = (items: ReturnType<typeof actionsFor>['more']) => items.map(i => (i === 'separator' ? '|' : i.id))

// Mirrors the backend's InvoiceLifecycle: only a draft is deleted, an issued invoice is cancelled
describe('actionsFor', () => {
  it('offers a draft finalizing and deleting, never cancelling', () => {
    const { primary, more } = actionsFor(invoice('Draft'))

    expect(primary?.id).toBe('finalize')
    expect(ids(more)).toContain('delete')
    expect(ids(more)).not.toContain('cancel')
  })

  it('moves an issued invoice forward; it can be cancelled, deleted only by force', () => {
    expect(actionsFor(invoice('Finalized')).primary?.id).toBe('markSent')
    expect(actionsFor(invoice('Sent')).primary?.id).toBe('markPaid')

    for (const status of ['Finalized', 'Sent']) {
      const more = ids(actionsFor(invoice(status)).more)
      expect(more).toContain('cancel')
      expect(more).toContain('forceDelete')
      expect(more).not.toContain('delete')
    }
  })

  it('leaves a paid or cancelled invoice with its documents only', () => {
    for (const status of ['Paid', 'Cancelled']) {
      const { primary, more } = actionsFor(invoice(status))
      expect(primary).toBeNull()
      expect(ids(more)).not.toContain('cancel')
      expect(ids(more)).not.toContain('markPaid')
    }
  })

  it('adds the monthly report actions to monthly invoices only', () => {
    expect(ids(actionsFor(invoice('Paid', 'Monthly')).more)).toEqual(['downloadPdf', 'downloadReport', 'regenerate', 'verifyReport', '|', 'forceDelete'])
    expect(ids(actionsFor(invoice('Paid', 'OneTime')).more)).toEqual(['downloadPdf', 'regenerate', '|', 'forceDelete'])
  })

  it('reads the status whether the API sent it as a name or a number', () => {
    expect(actionsFor({ ...invoice('Draft'), status: 1 }).primary?.id).toBe('markSent')
  })
})
