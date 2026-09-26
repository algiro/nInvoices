<template>
  <section class="section">
    <header class="section-header">
      <div>
        <h3>Projects</h3>
        <p class="help">Used to split worked days on invoices and monthly reports. New names typed while generating an invoice are added here automatically.</p>
      </div>
      <BaseButton variant="primary" icon="plus" @click="handleAdd">New project</BaseButton>
    </header>

    <LoadingState v-if="loading && projects.length === 0" label="Loading projects…" />

    <EmptyState v-else-if="error" icon="alert" title="Projects could not be loaded" :description="error" compact>
      <BaseButton @click="loadProjects">Try again</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="projects.length === 0"
      icon="folder"
      title="No projects"
      description="Projects are optional. Without them, each worked day is billed as a single line."
      compact
    >
      <BaseButton variant="primary" icon="plus" @click="handleAdd">Add a project</BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">Project</th>
            <th scope="col">Status</th>
            <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="project in projects" :key="project.id" :class="{ inactive: !project.isActive }">
            <td class="primary-cell">{{ project.name }}</td>
            <td>
              <StatusPill :tone="project.isActive ? 'success' : 'neutral'">{{ project.isActive ? 'Active' : 'Inactive' }}</StatusPill>
            </td>
            <td class="actions">
              <BaseButton size="sm" variant="ghost" icon="edit" @click="handleEdit(project)">Edit</BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                title="Delete"
                :aria-label="`Delete ${project.name}`"
                @click="handleDelete(project)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <BaseDialog
      :open="showForm"
      :title="editingProject ? 'Edit project' : 'New project'"
      size="sm"
      :close-on-overlay="false"
      @close="handleCloseForm"
    >
      <ProjectForm
        v-if="showForm"
        :customer-id="customerId"
        :project-id="editingProject?.id"
        @success="handleFormSuccess"
        @cancel="handleCloseForm"
      />
    </BaseDialog>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useProjectsStore } from '@/stores/projects'
import ProjectForm from './ProjectForm.vue'
import type { ProjectDto } from '@/types'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'

interface Props {
  customerId: number
}

const props = defineProps<Props>()
const projectsStore = useProjectsStore()
const toast = useToast()
const { confirm } = useConfirm()

const showForm = ref(false)
const editingProject = ref<ProjectDto | null>(null)

// Active projects first, then alphabetical
const projects = computed(() =>
  [...projectsStore.projectsByCustomer(props.customerId)].sort((a, b) =>
    Number(b.isActive) - Number(a.isActive) || a.name.localeCompare(b.name)
  )
)
const loading = computed(() => projectsStore.loading)
const error = computed(() => projectsStore.error)

onMounted(loadProjects)

async function loadProjects() {
  try {
    await projectsStore.fetchByCustomerId(props.customerId)
  } catch {
    // the store keeps the message; the empty state shows it
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
    } else {
      toast.success('Project deleted')
    }
  } catch (error: any) {
    toast.failure('Failed to delete project', error)
  }
}

function handleFormSuccess() {
  toast.success(editingProject.value ? 'Project updated' : 'Project added')
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
.section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.section-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
}

.section-header h3 {
  font-size: 1rem;
}

.help {
  margin: 0.2rem 0 0;
  max-width: 65ch;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

tr.inactive .primary-cell {
  color: var(--color-text-muted);
  font-weight: 500;
}
</style>
