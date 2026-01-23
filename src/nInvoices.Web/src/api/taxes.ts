import { apiClient } from './client'
import type { TaxDto, CreateTaxDto, UpdateTaxDto } from '@/types'

export const taxesApi = {
  async getAll(): Promise<TaxDto[]> {
    return apiClient.get<TaxDto[]>('/api/taxes')
  },

  async getById(id: number): Promise<TaxDto> {
    return apiClient.get<TaxDto>(`/api/taxes/${id}`)
  },

  async getByCustomerId(customerId: number): Promise<TaxDto[]> {
    return apiClient.get<TaxDto[]>(`/api/taxes/customer/${customerId}`)
  },

  async create(data: CreateTaxDto): Promise<TaxDto> {
    return apiClient.post<TaxDto>('/api/taxes', data)
  },

  async update(id: number, data: UpdateTaxDto): Promise<void> {
    return apiClient.put<void>(`/api/taxes/${id}`, data)
  },

  async delete(id: number): Promise<void> {
    return apiClient.delete<void>(`/api/taxes/${id}`)
  }
}