<template>
  <section class="section">
    <header class="section-header">
      <div>
        <h3>{{ t('customers.tabs.projects') }}</h3>
        <p class="help">{{ t('projects.help') }}</p>
      </div>
      <BaseButton variant="primary" icon="plus" @click="handleAdd">{{ t('projects.new') }}</BaseButton>
    </header>

    <LoadingState v-if="loading && projects.length === 0" :label="t('projects.loading')" />

    <EmptyState v-else-if="error" icon="alert" :title="t('projects.loadFailed')" :description="error" compact>
      <BaseButton @click="loadProjects">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="projects.length === 0"
      icon="folder"
      :title="t('projects.emptyTitle')"
      :description="t('projects.emptyDescription')"
      compact
    >
      <BaseButton variant="primary" icon="plus" @click="handleAdd">{{ t('projects.addOne') }}</BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">{{ t('projects.project') }}</th>
            <th scope="col">{{ t('invoices.columns.status') }}</th>
            <th scope="col" class="actions"><span class="sr-only">{{ t('common.actions') }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="project in projects" :key="project.id" :class="{ inactive: !project.isActive }">
            <td class="primary-cell">{{ project.name }}</td>
            <td>
              <StatusPill :tone="project.isActive ? 'success' : 'neutral'">{{ project.isActive ? t('common.active') : t('common.inactive') }}</StatusPill>
            </td>
            <td class="actions">
              <BaseButton size="sm" variant="ghost" icon="edit" @click="handleEdit(project)">{{ t('common.edit') }}</BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                :title="t('common.delete')"
                :aria-label="t('common.deleteNamed', { name: project.name })"
                @click="handleDelete(project)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <BaseDialog
      :open="showForm"
      :title="editingProject ? t('projects.edit') : t('projects.new')"
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
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

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
  if (!(await confirm({ title: t('projects.deleteTitle'), message: t('projects.deleteMessage', { name: project.name }), confirmLabel: t('common.delete'), tone: 'danger' }))) {
    return
  }

  try {
    const result = await projectsStore.remove(project.id)
    if (result.deactivated) {
      toast.info(t('projects.deactivated'), { message: t('projects.deactivatedMessage', { name: project.name }) })
    } else {
      toast.success(t('projects.deleted'))
    }
  } catch (error) {
    toast.failure(t('projects.deleteFailed'), error)
  }
}

function handleFormSuccess() {
  toast.success(editingProject.value ? t('projects.updated') : t('projects.added'))
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
