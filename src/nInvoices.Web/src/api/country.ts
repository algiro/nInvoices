import { apiClient } from './client'
import type { UserCountryDto } from '@/types'

export const countryApi = {
  /** The country the user works from (chosen, or inferred from what they use) and the countries offered */
  get: async (): Promise<UserCountryDto> => {
    return apiClient.get<UserCountryDto>('/api/country')
  },

  /** Sets it; the invoicing rules of the other countries are turned off, their settings kept */
  set: async (countryCode: string): Promise<UserCountryDto> => {
    return apiClient.put<UserCountryDto>('/api/country', { countryCode })
  }
}
