import { apiClient } from './client'
import type {
  TaxPredictionDto,
  TaxPredictionSettingsDto,
  TaxProfileDto,
  UpdateTaxProfileDto,
  TaxPaymentDto,
  RecordTaxPaymentDto
} from '@/types'

export const taxPredictionApi = {
  /** The estimate of a year, quarter by quarter; `isSetUp` is false until the user says where they pay taxes */
  get: async (year: number): Promise<TaxPredictionDto> => {
    return apiClient.get<TaxPredictionDto>(`/api/tax-prediction/${year}`)
  },

  /** The countries whose taxes can be estimated (regimes, fields, defaults) and the user's profile */
  getSettings: async (): Promise<TaxPredictionSettingsDto> => {
    return apiClient.get<TaxPredictionSettingsDto>('/api/tax-prediction/settings')
  },

  /** A missing required setting or an invalid choice comes back as a 400 with `issues` per field */
  updateProfile: async (dto: UpdateTaxProfileDto): Promise<TaxProfileDto> => {
    return apiClient.put<TaxProfileDto>('/api/tax-prediction/profile', dto)
  },

  /** Records what was paid for a form and quarter, replacing an earlier record of the same */
  recordPayment: async (dto: RecordTaxPaymentDto): Promise<TaxPaymentDto> => {
    return apiClient.put<TaxPaymentDto>('/api/tax-prediction/payments', dto)
  },

  deletePayment: async (id: number): Promise<void> => {
    return apiClient.delete<void>(`/api/tax-prediction/payments/${id}`)
  }
}
