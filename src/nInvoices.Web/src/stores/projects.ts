import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { projectsApi } from '@/api/projects'
import type {
  ProjectDto,
  CreateProjectDto,
  UpdateProjectDto,
  DeleteProjectResultDto
} from '@/types'

export const useProjectsStore = defineStore('projects', () => {
  const projects = ref<ProjectDto[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const projectsByCustomer = computed(() => {
    return (customerId: number) => projects.value.filter(p => p.customerId === customerId)
  })

  const activeProjectsByCustomer = computed(() => {
    return (customerId: number) =>
      projects.value.filter(p => p.customerId === customerId && p.isActive)
  })

  function upsert(list: ProjectDto[], customerId: number) {
    projects.value = [
      ...projects.value.filter(p => p.customerId !== customerId),
      ...list
    ]
  }

  async function fetchByCustomerId(customerId: number, includeInactive = true) {
    try {
      loading.value = true
      error.value = null
      const list = await projectsApi.getByCustomerId(customerId, includeInactive)
      upsert(list, customerId)
      return list
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch projects'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function create(data: CreateProjectDto) {
    try {
      loading.value = true
      error.value = null
      const created = await projectsApi.create(data)
      projects.value.push(created)
      return created
    } catch (err: any) {
      error.value = err.message || 'Failed to create project'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function update(id: number, data: UpdateProjectDto) {
    try {
      loading.value = true
      error.value = null
      const updated = await projectsApi.update(id, data)
      const index = projects.value.findIndex(p => p.id === id)
      if (index !== -1) {
        projects.value[index] = updated
      }
      return updated
    } catch (err: any) {
      error.value = err.message || 'Failed to update project'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function remove(id: number): Promise<DeleteProjectResultDto> {
    try {
      loading.value = true
      error.value = null
      const result = await projectsApi.delete(id)
      if (result.deleted) {
        projects.value = projects.value.filter(p => p.id !== id)
      } else if (result.deactivated) {
        const index = projects.value.findIndex(p => p.id === id)
        if (index !== -1) {
          projects.value[index] = { ...projects.value[index], isActive: false }
        }
      }
      return result
    } catch (err: any) {
      error.value = err.message || 'Failed to delete project'
      throw err
    } finally {
      loading.value = false
    }
  }

  return {
    projects,
    loading,
    error,
    projectsByCustomer,
    activeProjectsByCustomer,
    fetchByCustomerId,
    create,
    update,
    remove
  }
})
