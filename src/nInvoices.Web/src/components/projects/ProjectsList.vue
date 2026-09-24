<template>
  <div class="projects-list">
    <div class="list-header">
      <h3 class="text-lg font-semibold">Projects</h3>
      <button @click="handleAdd" class="btn-primary">
        <svg class="w-4 h-4 inline mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        Add Project
      </button>
    </div>

    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <p class="mt-2 text-sm text-gray-600">Loading projects...</p>
    </div>

    <div v-else-if="error" class="error-state">
      <p class="text-red-600 text-sm">{{ error }}</p>
      <button @click="loadProjects" class="btn-secondary mt-2">Retry</button>
    </div>

    <div v-else-if="projects.length === 0" class="empty-state">
      <p class="text-gray-600 text-sm">No projects yet. Projects are also created automatically when you type a new name on the invoice calendar.</p>
      <button @click="handleAdd" class="btn-primary mt-2">Add First Project</button>
    </div>

    <table v-else class="projects-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Status</th>
          <th class="actions-col"></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="project in projects" :key="project.id" :class="{ inactive: !project.isActive }">
          <td class="name-cell">{{ project.name }}</td>
          <td>
            <span class="status-badge" :class="project.isActive ? 'active' : 'inactive'">
              {{ project.isActive ? 'Active' : 'Inactive' }}
            </span>
          </td>
          <td class="actions-cell">
            <button @click="handleEdit(project)" class="action-btn" title="Edit project">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
              </svg>
            </button>
            <button @click="handleDelete(project)" class="action-btn danger" title="Delete project">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
            </button>
          </td>
        </tr>
      </tbody>
    </table>

    <teleport to="body">
      <div v-if="showForm" class="modal-overlay" @click="handleCloseForm">
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h3 class="text-xl font-semibold">{{ editingProject ? 'Edit Project' : 'Add Project' }}</h3>
            <button @click="handleCloseForm" class="close-btn">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
          <ProjectForm
            :customer-id="customerId"
            :project-id="editingProject?.id"
            @success="handleFormSuccess"
            @cancel="handleCloseForm"
          />
        </div>
      </div>
    </teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useProjectsStore } from '@/stores/projects'
import ProjectForm from './ProjectForm.vue'
import type { ProjectDto } from '@/types'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'

const toast = useToast()
const { confirm } = useConfirm()

interface Props {
  customerId: number
}

const props = defineProps<Props>()
const projectsStore = useProjectsStore()

const showForm = ref(false)
const editingProject = ref<ProjectDto | null>(null)

const projects = computed(() =>
  [...projectsStore.projectsByCustomer(props.customerId)].sort((a, b) =>
    a.name.localeCompare(b.name)
  )
)
const loading = computed(() => projectsStore.loading)
const error = computed(() => projectsStore.error)

onMounted(loadProjects)

async function loadProjects() {
  try {
    await projectsStore.fetchByCustomerId(props.customerId)
  } catch (err) {
    console.error('Failed to load projects:', err)
  }
}

function handleAdd() {
  editingProject.value = null
  showForm.value = true
}

function handleEdit(project: ProjectDto) {
  editingProject.value = project
  showForm.value = true
}

async function handleDelete(project: ProjectDto) {
  if (!(await confirm({ title: 'Delete project?', message: `"${project.name}" will be deleted. If it is used on any work day it is deactivated instead.`, confirmLabel: 'Delete', tone: 'danger' }))) {
    return
  }

  try {
    const result = await projectsStore.remove(project.id)
    if (result.deactivated) {
      toast.info('Project deactivated', { message: `"${project.name}" is used on existing work days, so it was deactivated instead of deleted.` })
    }
  } catch (error: any) {
    toast.failure('Failed to delete project', error)
  }
}

function handleFormSuccess() {
  showForm.value = false
  editingProject.value = null
  loadProjects()
}

function handleCloseForm() {
  showForm.value = false
  editingProject.value = null
}
</script>

<style scoped>
.projects-list {
  padding: 1rem;
}

.list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
}

.loading-state,
.error-state,
.empty-state {
  text-align: center;
  padding: 3rem 1rem;
}

.spinner {
  border: 3px solid #f3f4f6;
  border-top: 3px solid #2563eb;
  border-radius: 50%;
  width: 30px;
  height: 30px;
  animation: spin 1s linear infinite;
  margin: 0 auto;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.projects-table {
  width: 100%;
  border-collapse: collapse;
}

.projects-table th {
  text-align: left;
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #6b7280;
  padding: 0.5rem 0.75rem;
  border-bottom: 1px solid #e5e7eb;
}

.projects-table td {
  padding: 0.75rem;
  border-bottom: 1px solid #f3f4f6;
}

.projects-table tr.inactive .name-cell {
  color: #9ca3af;
  text-decoration: line-through;
}

.name-cell {
  font-weight: 500;
  color: #1f2937;
}

.status-badge {
  display: inline-block;
  padding: 0.125rem 0.625rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
}

.status-badge.active {
  background: #d1fae5;
  color: #065f46;
}

.status-badge.inactive {
  background: #f3f4f6;
  color: #6b7280;
}

.actions-col {
  width: 5rem;
}

.actions-cell {
  text-align: right;
  white-space: nowrap;
}

.action-btn {
  padding: 0.375rem;
  border: none;
  background: transparent;
  border-radius: 0.375rem;
  cursor: pointer;
  color: #6b7280;
  transition: all 0.2s;
}

.action-btn:hover {
  background: #f3f4f6;
  color: #1f2937;
}

.action-btn.danger:hover {
  background: #fee2e2;
  color: #dc2626;
}

.btn-primary,
.btn-secondary {
  padding: 0.5rem 1rem;
  border-radius: 0.375rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
  font-size: 0.875rem;
}

.btn-primary {
  background: #2563eb;
  color: white;
}

.btn-primary:hover {
  background: #1d4ed8;
}

.btn-secondary {
  background: white;
  color: #374151;
  border: 1px solid #d1d5db;
}

.btn-secondary:hover {
  background: #f9fafb;
}

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: white;
  border-radius: 0.5rem;
  max-width: 500px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  border-bottom: 1px solid #e5e7eb;
}

.close-btn {
  padding: 0.5rem;
  border: none;
  background: transparent;
  border-radius: 0.375rem;
  cursor: pointer;
  color: #6b7280;
  transition: all 0.2s;
}

.close-btn:hover {
  background: #f3f4f6;
  color: #1f2937;
}
</style>
