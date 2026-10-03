import { apiClient } from './client';
import type { ChainReportDto, InvoiceVerifactuDto, VerifactuStatusDto, VerifactuSubmissionRunDto } from '@/types';

export const verifactuApi = {
  /** Where an invoice stands in the chain: its record, hash and QR code */
  getInvoice: async (invoiceId: number): Promise<InvoiceVerifactuDto> => {
    return apiClient.get<InvoiceVerifactuDto>(`/api/verifactu/invoices/${invoiceId}`);
  },

  /** How many records are waiting, accepted or rejected by the Tax Agency */
  getStatus: async (): Promise<VerifactuStatusDto> => {
    return apiClient.get<VerifactuStatusDto>('/api/verifactu/status');
  },

  /** Sends the waiting records now instead of at the next automatic round */
  submit: async (): Promise<VerifactuSubmissionRunDto> => {
    return apiClient.post<VerifactuSubmissionRunDto>('/api/verifactu/submit');
  },

  /** Checks the whole chain; anything changed or removed afterwards shows up as a problem */
  verifyChain: async (): Promise<ChainReportDto> => {
    return apiClient.get<ChainReportDto>('/api/verifactu/chain/verify');
  }
};
