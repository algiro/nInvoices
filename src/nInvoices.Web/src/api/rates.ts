import { apiClient } from './client'
import type { RateDto, CreateRateDto, UpdateRateDto } from '@/types'

export const ratesApi = {
  async getAll(): Promise<RateDto[]> {
    return apiClient.get<RateDto[]>('/api/rates')
  },

  async getById(id: number): Promise<RateDto> {
    return apiClient.get<RateDto>(`/api/rates/${id}`)
  },

  async getByCustomerId(customerId: number): Promise<RateDto[]> {
    return apiClient.get<RateDto[]>(`/api/rates/customer/${customerId}`)
  },

  async create(data: CreateRateDto): Promise<RateDto> {
    return apiClient.post<RateDto>('/api/rates', data)
  },

  async update(id: number, data: UpdateRateDto): Promise<void> {
    return apiClient.put<void>(`/api/rates/${id}`, data)
  },

  async delete(id: number): Promise<void> {
    return apiClient.delete<void>(`/api/rates/${id}`)
  }
}