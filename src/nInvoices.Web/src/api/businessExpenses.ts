import { apiClient } from './client'
import type { BusinessExpenseDto, SaveBusinessExpenseDto, RecurringExpenseDto, SaveRecurringExpenseDto } from '@/types'

/** The costs of the user's business, deducted in the tax prediction (not the expenses billed to a customer) */
export const businessExpensesApi = {
  /** The expenses dated in a year, oldest first */
  getByYear: async (year: number): Promise<BusinessExpenseDto[]> => {
    return apiClient.get<BusinessExpenseDto[]>('/api/business-expenses', { year })
  },

  create: async (dto: SaveBusinessExpenseDto): Promise<BusinessExpenseDto> => {
    return apiClient.post<BusinessExpenseDto>('/api/business-expenses', dto)
  },

  update: async (id: number, dto: SaveBusinessExpenseDto): Promise<BusinessExpenseDto> => {
    return apiClient.put<BusinessExpenseDto>(`/api/business-expenses/${id}`, dto)
  },

  delete: async (id: number): Promise<void> => {
    return apiClient.delete<void>(`/api/business-expenses/${id}`)
  },

  /** Expenses paid every month, such as the social security fee */
  getRecurring: async (): Promise<RecurringExpenseDto[]> => {
    return apiClient.get<RecurringExpenseDto[]>('/api/business-expenses/recurring')
  },

  createRecurring: async (dto: SaveRecurringExpenseDto): Promise<RecurringExpenseDto> => {
    return apiClient.post<RecurringExpenseDto>('/api/business-expenses/recurring', dto)
  },

  updateRecurring: async (id: number, dto: SaveRecurringExpenseDto): Promise<RecurringExpenseDto> => {
    return apiClient.put<RecurringExpenseDto>(`/api/business-expenses/recurring/${id}`, dto)
  },

  deleteRecurring: async (id: number): Promise<void> => {
    return apiClient.delete<void>(`/api/business-expenses/recurring/${id}`)
  }
}
