import { apiClient } from './client';

export interface DataExport {
  exportVersion: string;
  exportedAt: string;
  customers?: any[];
  invoices?: any[];
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

  async exportInvoices(params?: { year?: number; month?: number; customerId?: number }): Promise<DataExport> {
    return apiClient.get<DataExport>('/api/importexport/invoices', params);
  },

  async importCustomers(data: DataExport): Promise<ImportResult> {
    return apiClient.post<ImportResult>('/api/importexport/customers', data);
  },

  async importInvoices(data: DataExport): Promise<ImportResult> {
    return apiClient.post<ImportResult>('/api/importexport/invoices', data);
  },
};
