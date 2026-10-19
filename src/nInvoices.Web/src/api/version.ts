import { apiClient } from './client'
import type { AppVersionDto } from '@/types'

export const versionApi = {
  /** The release and the commit the running API was built from */
  get: async (): Promise<AppVersionDto> => {
    return apiClient.get<AppVersionDto>('/api/version')
  }
}
