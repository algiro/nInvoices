import { apiClient } from './client';
import type { 
  InvoiceDto, 
  GenerateInvoiceDto,
  InvoiceDraftPreviewDto,
  UpdateInvoiceDto,
  InvoiceEmailComposeDto,
  CreateInvoiceEmailDraftDto,
  InvoiceEmailDto
} from '../types';

/**
 * Invoices API Service
 * Encapsulates all invoice-related API calls
 */
export const invoicesApi = {
  async getAll(): Promise<InvoiceDto[]> {
    return apiClient.get<InvoiceDto[]>('/api/invoices');
  },

  async getById(id: number): Promise<InvoiceDto> {
    return apiClient.get<InvoiceDto>(`/api/invoices/${id}`);
  },

  async getByCustomer(customerId: number): Promise<InvoiceDto[]> {
    return apiClient.get<InvoiceDto[]>(`/api/invoices/customer/${customerId}`);
  },

  async getByPeriod(customerId: number, year: number, month?: number): Promise<InvoiceDto[]> {
    const params = { year, ...(month && { month }) };
    return apiClient.get<InvoiceDto[]>(`/api/invoices/customer/${customerId}/period`, params);
  },

  async generate(data: GenerateInvoiceDto): Promise<InvoiceDto> {
    return apiClient.post<InvoiceDto>('/api/invoices', data);
  },

  /** Renders the invoice and timesheet `data` would produce, without saving anything. */
  async previewDraft(data: GenerateInvoiceDto): Promise<InvoiceDraftPreviewDto> {
    return apiClient.post<InvoiceDraftPreviewDto>('/api/invoices/preview', data);
  },

  async update(id: number, data: UpdateInvoiceDto): Promise<void> {
    return apiClient.put<void>(`/api/invoices/${id}`, data);
  },

  async finalize(id: number): Promise<void> {
    return apiClient.post<void>(`/api/invoices/${id}/finalize`);
  },

  async markAsSent(id: number): Promise<void> {
    return apiClient.post<void>(`/api/invoices/${id}/mark-as-sent`);
  },

  async markAsPaid(id: number): Promise<void> {
    return apiClient.post<void>(`/api/invoices/${id}/mark-as-paid`);
  },

  async cancel(id: number): Promise<void> {
    return apiClient.post<void>(`/api/invoices/${id}/cancel`);
  },

  async delete(id: number, force: boolean = false): Promise<void> {
    console.log(`[API] delete called - ID: ${id}, Force: ${force}`);
    const params = force ? { force: true } : undefined;
    console.log('[API] params:', params);
    return apiClient.delete<void>(`/api/invoices/${id}`, params);
  },

  async downloadPdf(id: number): Promise<Blob> {
    return apiClient.downloadFile(`/api/invoices/${id}/pdf`);
  },

  async downloadCalendarPdf(id: number): Promise<Blob> {
    return apiClient.downloadFile(`/api/invoices/${id}/calendar/pdf`);
  },

  async downloadMonthlyReportPdf(id: number): Promise<Blob> {
    return apiClient.downloadFile(`/api/invoices/${id}/monthlyreport/pdf`);
  },

  async regenerateInvoicePdf(id: number): Promise<{ message: string }> {
    return apiClient.post(`/api/invoices/${id}/regenerate`, {});
  },

  async regenerateMonthlyReportPdf(id: number): Promise<{ message: string }> {
    return apiClient.post(`/api/invoices/${id}/monthlyreport/regenerate`, {});
  },

  async getSequence(): Promise<{ currentValue: number }> {
    return apiClient.get<{ currentValue: number }>('/api/invoices/sequence');
  },

  async setSequence(value: number): Promise<{ currentValue: number; message: string }> {
    return apiClient.put<{ currentValue: number; message: string }>('/api/invoices/sequence', { value });
  },

  /** The email for this invoice, rendered from the customer's active (or the given) email template. */
  async composeEmail(id: number, templateId?: number | null): Promise<InvoiceEmailComposeDto> {
    return apiClient.get<InvoiceEmailComposeDto>(`/api/invoices/${id}/email`, templateId ? { templateId } : undefined);
  },

  /** Creates a draft in the connected Gmail account with the invoice documents attached. */
  async createEmailDraft(id: number, data: CreateInvoiceEmailDraftDto): Promise<InvoiceEmailDto> {
    return apiClient.post<InvoiceEmailDto>(`/api/invoices/${id}/email/draft`, data);
  },

  /** Gmail drafts created for this invoice, newest first. */
  async getEmails(id: number): Promise<InvoiceEmailDto[]> {
    return apiClient.get<InvoiceEmailDto[]>(`/api/invoices/${id}/emails`);
  },
};
