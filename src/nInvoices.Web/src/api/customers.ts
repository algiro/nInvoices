import { apiClient } from './client';
import type { CustomerDto, CreateCustomerDto, UpdateCustomerDto } from '../types';

/**
 * Customers API Service
 * Encapsulates all customer-related API calls
 */
export const customersApi = {
  async getAll(): Promise<CustomerDto[]> {
    return apiClient.get<CustomerDto[]>('/api/customers');
  },

  async getById(id: number): Promise<CustomerDto> {
    return apiClient.get<CustomerDto>(`/api/customers/${id}`);
  },

  async create(data: CreateCustomerDto): Promise<CustomerDto> {
    return apiClient.post<CustomerDto>('/api/customers', data);
  },

  async update(id: number, data: UpdateCustomerDto): Promise<void> {
    return apiClient.put<void>(`/api/customers/${id}`, data);
  },

  async delete(id: number): Promise<void> {
    return apiClient.delete<void>(`/api/customers/${id}`);
  },
};
