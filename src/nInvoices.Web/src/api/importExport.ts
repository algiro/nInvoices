import { apiClient } from './client';

export interface DataExport {
  exportVersion: string;
  exportedAt: string;
  customers?: unknown[];
  invoices?: unknown[];
  /** Templates shared by all customers; absent from exports made before they existed. */
  sharedTemplates?: unknown;
  /** Invoice numbering, template images, holiday calendars, e-invoicing settings. */
  settings?: {
    invoiceNumbering?: unknown;
    images?: unknown[];
    holidayCalendars?: unknown[];
    compliance?: unknown[];
  } | null;
}

export interface ImportResult {
  imported: number;
  skipped: number;
  errors: string[];
}

/**
 * Import/Export API Service
 * Handles data migration and backup operations
 */
export const importExportApi = {
  async exportCustomers(): Promise<DataExport> {
    return apiClient.get<DataExport>('/api/importexport/customers');
  },

  async exportSettings(): Promise<DataExport> {
    return apiClient.get<DataExport>('/api/importexport/settings');
  },

  async exportInvoices(params?: { year?: number; month?: number; customerId?: number }): Promise<DataExport> {
    return apiClient.get<DataExport>('/api/importexport/invoices', params);
  },

  async importCustomers(data: DataExport): Promise<ImportResult> {
    return apiClient.post<ImportResult>('/api/importexport/customers', data);
  },

  async importSettings(data: DataExport): Promise<ImportResult> {
    return apiClient.post<ImportResult>('/api/importexport/settings', data);
  },

  async importInvoices(data: DataExport): Promise<ImportResult> {
    return apiClient.post<ImportResult>('/api/importexport/invoices', data);
  },
};
