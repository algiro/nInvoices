import { apiClient } from './client';
import type { 
  InvoiceDto, 
  GenerateInvoiceDto, 
  UpdateInvoiceDto 
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

  async update(id: number, data: UpdateInvoiceDto): Promise<void> {
    return apiClient.put<void>(`/api/invoices/${id}`, data);
  },

  async finalize(id: number): Promise<void> {
    return apiClient.post<void>(`/api/invoices/${id}/finalize`);
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
};
