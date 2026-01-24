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

  async delete(id: number): Promise<void> {
    return apiClient.delete<void>(`/api/invoices/${id}`);
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
};
