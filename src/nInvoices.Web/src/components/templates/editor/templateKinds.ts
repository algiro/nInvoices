import { templatesApi } from '@/api/templates'
import { monthlyReportTemplatesApi } from '@/api/monthlyReportTemplates'
import { emailTemplatesApi } from '@/api/emailTemplates'
import type { InvoiceType } from '@/types'
import type { TemplateKind } from './templateVariables'

export type InvoiceTypeName = 'Monthly' | 'OneTime'

/** What the editor edits; `subject` is for email templates, `invoiceType` for invoice templates. */
export interface TemplateDraft {
  name: string
  content: string
  subject: string
  invoiceType: InvoiceTypeName
}

/** A saved template; only the fields its kind has are set. */
export interface LoadedTemplate {
  name: string
  content: string
  isActive: boolean
  subject?: string
  invoiceType?: InvoiceTypeName
}

export interface TemplatePreview {
  html: string | null
  /** The rendered subject (email templates only). */
  subject: string | null
  errors: string[]
}

/**
 * The editor's view of one kind of template: the three kinds live behind different endpoints with
 * different fields, so the editor itself never branches on the kind to load, preview or save.
 */
export interface TemplateKindApi {
  load(id: number): Promise<LoadedTemplate>
  preview(content: string, subject: string, customerId: number | null): Promise<TemplatePreview>
  /** Returns the new template's id, and whether it started active when the server decides that. */
  create(draft: TemplateDraft, customerId: number | null): Promise<{ id: number; isActive?: boolean }>
  /** `isActive` keeps the template's current state where the API would otherwise reset it. */
  update(id: number, draft: TemplateDraft, isActive: boolean): Promise<void>
}

const invoice: TemplateKindApi = {
  async load(id) {
    const t = await templatesApi.getById(id)
    return { name: t.name, content: t.content, isActive: t.isActive, invoiceType: (String(t.invoiceType) as InvoiceTypeName) ?? 'Monthly' }
  },
  async preview(content, _subject, customerId) {
    const result = await templatesApi.preview(content, customerId ?? undefined)
    return { html: result.html, subject: null, errors: result.errors }
  },
  async create(draft, customerId) {
    const created = await templatesApi.create({
      customerId,
      invoiceType: draft.invoiceType as unknown as InvoiceType,
      name: draft.name,
      content: draft.content
    })
    return { id: created.id }
  },
  async update(id, draft, isActive) {
    await templatesApi.update(id, {
      invoiceType: draft.invoiceType as unknown as InvoiceType,
      name: draft.name,
      content: draft.content,
      isActive // the API defaults to active
    })
  }
}

const monthlyReport: TemplateKindApi = {
  async load(id) {
    const t = await monthlyReportTemplatesApi.getById(id)
    return { name: t.name, content: t.content, isActive: t.isActive }
  },
  async preview(content, _subject, customerId) {
    const result = await monthlyReportTemplatesApi.preview(content, customerId ?? undefined)
    return { html: result.html, subject: null, errors: result.errors }
  },
  async create(draft, customerId) {
    const created = await monthlyReportTemplatesApi.create({ customerId, name: draft.name, content: draft.content })
    return { id: created.id }
  },
  async update(id, draft) {
    await monthlyReportTemplatesApi.update(id, { name: draft.name, content: draft.content })
  }
}

const email: TemplateKindApi = {
  async load(id) {
    const t = await emailTemplatesApi.getById(id)
    return { name: t.name, content: t.body, subject: t.subject, isActive: t.isActive }
  },
  async preview(content, subject, customerId) {
    return emailTemplatesApi.preview(subject, content, customerId)
  },
  async create(draft, customerId) {
    const created = await emailTemplatesApi.create({ customerId, name: draft.name, subject: draft.subject, body: draft.content })
    return { id: created.id, isActive: created.isActive }
  },
  async update(id, draft) {
    await emailTemplatesApi.update(id, { name: draft.name, subject: draft.subject, body: draft.content })
  }
}

export const templateKinds: Record<TemplateKind, TemplateKindApi> = {
  invoice,
  'monthly-report': monthlyReport,
  email
}
