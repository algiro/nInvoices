import { apiClient } from './client';
import type { GmailStatusDto } from '@/types';

export const gmailApi = {
  getStatus: async (): Promise<GmailStatusDto> => {
    return apiClient.get<GmailStatusDto>('/api/gmail/status');
  },

  /** Google consent URL; navigate the browser there to connect the account. */
  connect: async (): Promise<{ authorizationUrl: string }> => {
    return apiClient.post<{ authorizationUrl: string }>('/api/gmail/connect');
  },

  disconnect: async (): Promise<void> => {
    return apiClient.delete<void>('/api/gmail/connection');
  }
};
