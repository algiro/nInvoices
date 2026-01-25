import { apiClient } from './client'
import type { 
  InvoiceTemplateDto, 
  CreateInvoiceTemplateDto, 
  UpdateInvoiceTemplateDto,
  TemplateValidationResultDto 
} from '@/types'

export const templatesApi = {
  async getAll(): Promise<InvoiceTemplateDto[]> {
    return apiClient.get<InvoiceTemplateDto[]>('/api/invoicetemplates')
  },

  async getById(id: number): Promise<InvoiceTemplateDto> {
    return apiClient.get<InvoiceTemplateDto>(`/api/invoicetemplates/${id}`)
  },

  async getByCustomerId(customerId: number): Promise<InvoiceTemplateDto[]> {
    return apiClient.get<InvoiceTemplateDto[]>(`/api/invoicetemplates/customer/${customerId}`)
  },

  async getByCustomerAndType(customerId: number, invoiceType: string): Promise<InvoiceTemplateDto | null> {
    try {
      return await apiClient.get<InvoiceTemplateDto>(
        `/api/invoicetemplates/customer/${customerId}/type/${invoiceType}`
      )
    } catch (error) {
      return null
    }
  },

  async create(data: CreateInvoiceTemplateDto): Promise<InvoiceTemplateDto> {
    return apiClient.post<InvoiceTemplateDto>('/api/invoicetemplates', data)
  },

  async update(id: number, data: UpdateInvoiceTemplateDto): Promise<void> {
    return apiClient.put<void>(`/api/invoicetemplates/${id}`, data)
  },

  async delete(id: number): Promise<void> {
    return apiClient.delete<void>(`/api/invoicetemplates/${id}`)
  },

  async validate(content: string): Promise<TemplateValidationResultDto> {
    return apiClient.post<TemplateValidationResultDto>('/api/invoicetemplates/validate', content)
  }
}