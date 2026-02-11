import { apiClient } from './client'

export interface ImageAssetDto {
  id: number
  alias: string
  fileName: string
  contentType: string
  fileSize: number
  createdAt: string
  updatedAt: string
}

export interface ImageAssetWithDataDto extends ImageAssetDto {
  base64Data: string
}

export const imageAssetsApi = {
  async getAll(): Promise<ImageAssetDto[]> {
    return apiClient.get<ImageAssetDto[]>('/api/imageassets')
  },

  async getById(id: number): Promise<ImageAssetWithDataDto> {
    return apiClient.get<ImageAssetWithDataDto>(`/api/imageassets/${id}`)
  },

  async upload(alias: string, file: File): Promise<ImageAssetDto> {
    const formData = new FormData()
    formData.append('alias', alias)
    formData.append('file', file)
    return apiClient.post<ImageAssetDto>('/api/imageassets', formData)
  },

  async update(id: number, alias?: string, file?: File): Promise<ImageAssetDto> {
    const formData = new FormData()
    if (alias) formData.append('alias', alias)
    if (file) formData.append('file', file)
    return apiClient.put<ImageAssetDto>(`/api/imageassets/${id}`, formData)
  },

  async delete(id: number): Promise<void> {
    await apiClient.delete(`/api/imageassets/${id}`)
  }
}
