import { apiClient } from './client';
import type {
  MonthlyReportTemplateDto,
  CreateMonthlyReportTemplateDto,
  UpdateMonthlyReportTemplateDto,
  TemplateValidationResultDto
} from '@/types';

export const monthlyReportTemplatesApi = {
  getByCustomer: async (customerId: number): Promise<MonthlyReportTemplateDto[]> => {
    return apiClient.get<MonthlyReportTemplateDto[]>(`/api/monthlyreporttemplates/customer/${customerId}`);
  },

  getById: async (id: number): Promise<MonthlyReportTemplateDto> => {
    return apiClient.get<MonthlyReportTemplateDto>(`/api/monthlyreporttemplates/${id}`);
  },

  create: async (dto: CreateMonthlyReportTemplateDto): Promise<MonthlyReportTemplateDto> => {
    return apiClient.post<MonthlyReportTemplateDto>('/api/monthlyreporttemplates', dto);
  },

  update: async (id: number, dto: UpdateMonthlyReportTemplateDto): Promise<MonthlyReportTemplateDto> => {
    return apiClient.put<MonthlyReportTemplateDto>(`/api/monthlyreporttemplates/${id}`, dto);
  },

  delete: async (id: number): Promise<void> => {
    return apiClient.delete<void>(`/api/monthlyreporttemplates/${id}`);
  },

  activate: async (id: number): Promise<void> => {
    return apiClient.post<void>(`/api/monthlyreporttemplates/${id}/activate`);
  },

  deactivate: async (id: number): Promise<void> => {
    return apiClient.post<void>(`/api/monthlyreporttemplates/${id}/deactivate`);
  },

  validate: async (content: string): Promise<TemplateValidationResultDto> => {
    return apiClient.post<TemplateValidationResultDto>('/api/monthlyreporttemplates/validate', content);
  }
};
