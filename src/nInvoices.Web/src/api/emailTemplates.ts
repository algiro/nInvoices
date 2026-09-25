import { apiClient } from './client';
import type {
  EmailTemplateDto,
  CreateEmailTemplateDto,
  UpdateEmailTemplateDto,
  EmailTemplatePreviewDto
} from '@/types';

export const emailTemplatesApi = {
  getByCustomer: async (customerId: number): Promise<EmailTemplateDto[]> => {
    return apiClient.get<EmailTemplateDto[]>(`/api/emailtemplates/customer/${customerId}`);
  },

  getById: async (id: number): Promise<EmailTemplateDto> => {
    return apiClient.get<EmailTemplateDto>(`/api/emailtemplates/${id}`);
  },

  /** The built-in subject and body, as a starting point. */
  getDefault: async (): Promise<UpdateEmailTemplateDto> => {
    return apiClient.get<UpdateEmailTemplateDto>('/api/emailtemplates/default');
  },

  create: async (dto: CreateEmailTemplateDto): Promise<EmailTemplateDto> => {
    return apiClient.post<EmailTemplateDto>('/api/emailtemplates', dto);
  },

  update: async (id: number, dto: UpdateEmailTemplateDto): Promise<EmailTemplateDto> => {
    return apiClient.put<EmailTemplateDto>(`/api/emailtemplates/${id}`, dto);
  },

  delete: async (id: number): Promise<void> => {
    return apiClient.delete<void>(`/api/emailtemplates/${id}`);
  },

  activate: async (id: number): Promise<void> => {
    return apiClient.post<void>(`/api/emailtemplates/${id}/activate`);
  },

  deactivate: async (id: number): Promise<void> => {
    return apiClient.post<void>(`/api/emailtemplates/${id}/deactivate`);
  },

  /** Renders subject and body with the customer's latest invoice, without saving. */
  preview: async (subject: string, body: string, customerId: number): Promise<EmailTemplatePreviewDto> => {
    return apiClient.post<EmailTemplatePreviewDto>('/api/emailtemplates/preview', { subject, body, customerId });
  }
};
