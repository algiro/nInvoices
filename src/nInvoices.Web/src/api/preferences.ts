import { apiClient } from './client'
import type { UpdateUserPreferencesDto, UserPreferencesDto } from '@/types'

export const preferencesApi = {
  /** The user's preferences (the app's language) and the languages the app is available in */
  get: async (): Promise<UserPreferencesDto> => {
    return apiClient.get<UserPreferencesDto>('/api/preferences')
  },

  /** Changes them; they follow the user to every device */
  update: async (preferences: UpdateUserPreferencesDto): Promise<UserPreferencesDto> => {
    return apiClient.put<UserPreferencesDto>('/api/preferences', preferences)
  }
}
