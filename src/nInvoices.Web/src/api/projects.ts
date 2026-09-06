import { apiClient } from './client'
import type {
  ProjectDto,
  CreateProjectDto,
  UpdateProjectDto,
  DeleteProjectResultDto
} from '@/types'

export const projectsApi = {
  async getAll(): Promise<ProjectDto[]> {
    return apiClient.get<ProjectDto[]>('/api/projects')
  },

  async getById(id: number): Promise<ProjectDto> {
    return apiClient.get<ProjectDto>(`/api/projects/${id}`)
  },

  async getByCustomerId(customerId: number, includeInactive = true): Promise<ProjectDto[]> {
    return apiClient.get<ProjectDto[]>(
      `/api/projects/customer/${customerId}?includeInactive=${includeInactive}`
    )
  },

  async create(data: CreateProjectDto): Promise<ProjectDto> {
    return apiClient.post<ProjectDto>('/api/projects', data)
  },

  async update(id: number, data: UpdateProjectDto): Promise<ProjectDto> {
    return apiClient.put<ProjectDto>(`/api/projects/${id}`, data)
  },

  async delete(id: number): Promise<DeleteProjectResultDto> {
    return apiClient.delete<DeleteProjectResultDto>(`/api/projects/${id}`)
  }
}
