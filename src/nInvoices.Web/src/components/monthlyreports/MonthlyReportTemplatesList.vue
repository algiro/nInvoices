<template>
  <div class="templates-list">
    <div class="list-header">
      <h3 class="text-lg font-semibold">Monthly Report Templates</h3>
      <button @click="handleAdd" class="btn-primary">
        <svg class="w-4 h-4 inline mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        Add Template
      </button>
    </div>

    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <p class="mt-2 text-sm text-gray-600">Loading templates...</p>
    </div>

    <div v-else-if="error" class="error-state">
      <p class="text-red-600 text-sm">{{ error }}</p>
      <button @click="loadTemplates" class="btn-secondary mt-2">Retry</button>
    </div>

    <div v-else-if="templates.length === 0" class="empty-state">
      <svg class="w-16 h-16 mx-auto text-gray-400 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
      </svg>
      <p class="text-gray-600 text-sm mb-2">No monthly report templates configured yet</p>
      <p class="text-gray-500 text-xs mb-4">Create a template to generate monthly work reports</p>
      <button @click="handleAdd" class="btn-primary">Create First Template</button>
    </div>

    <div v-else class="templates-cards">
      <div v-for="template in templates" :key="template.id" class="template-card">
        <div class="card-header">
          <div>
            <h4 class="card-title">{{ template.name }}</h4>
            <p class="card-subtitle">
              Created {{ formatDate(template.createdAt) }}
              <span v-if="template.isActive" class="active-badge">Active</span>
            </p>
          </div>
          <div class="card-actions">
            <button
              v-if="!template.isActive"
              @click="handleActivate(template)"
              class="action-btn text-green-600 hover:bg-green-50"
              title="Activate template"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
              </svg>
            </button>
            <button
              v-else
              @click="handleDeactivate(template)"
              class="action-btn text-gray-600 hover:bg-gray-50"
              title="Deactivate template"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" />
              </svg>
            </button>
            <button
              @click="handleEdit(template)"
              class="action-btn"
              title="Edit template"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
              </svg>
            </button>
            <button
              @click="handleDelete(template)"
              class="action-btn text-red-600 hover:bg-red-50"
              title="Delete template"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
            </button>
          </div>
        </div>
        <div class="card-content">
          <pre class="template-preview" v-text="truncateContent(template.content)"></pre>
        </div>
      </div>
    </div>

    <teleport to="body">
      <div v-if="showForm" class="modal-overlay" @click="handleCloseForm">
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h3 class="text-xl font-semibold">{{ editingTemplate ? 'Edit Template' : 'Add Template' }}</h3>
            <button @click="handleCloseForm" class="close-btn">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
          <MonthlyReportTemplateForm
            :customer-id="customerId"
            :template-id="editingTemplate?.id"
            @success="handleFormSuccess"
            @cancel="handleCloseForm"
          />
        </div>
      </div>
    </teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useMonthlyReportTemplatesStore } from '@/stores/monthlyReportTemplates'
import type { MonthlyReportTemplateDto } from '@/types'
import MonthlyReportTemplateForm from './MonthlyReportTemplateForm.vue'

const props = defineProps<{
  customerId: number
}>()

const store = useMonthlyReportTemplatesStore()
const showForm = ref(false)
const editingTemplate = ref<MonthlyReportTemplateDto | null>(null)

const templates = computed(() => store.templates)
const loading = computed(() => store.loading)
const error = computed(() => store.error)

watch(() => props.customerId, () => {
  if (props.customerId) {
    loadTemplates()
  }
})

onMounted(() => {
  if (props.customerId) {
    loadTemplates()
  }
})

async function loadTemplates() {
  console.log('loadTemplates called for customer', props.customerId);
  try {
    await store.fetchByCustomer(props.customerId)
    console.log('Templates loaded:', store.templates.length);
  } catch (err) {
    console.error('Failed to load templates:', err)
  }
}

function handleAdd() {
  editingTemplate.value = null
  showForm.value = true
}

function handleEdit(template: MonthlyReportTemplateDto) {
  editingTemplate.value = template
  showForm.value = true
}

async function handleActivate(template: MonthlyReportTemplateDto) {
  try {
    await store.activate(template.id)
    await loadTemplates()
  } catch (err: any) {
    alert(`Failed to activate template: ${err.message}`)
  }
}

async function handleDeactivate(template: MonthlyReportTemplateDto) {
  try {
    await store.deactivate(template.id)
    await loadTemplates()
  } catch (err: any) {
    alert(`Failed to deactivate template: ${err.message}`)
  }
}

async function handleDelete(template: MonthlyReportTemplateDto) {
  if (!confirm(`Are you sure you want to delete the template "${template.name}"?`)) {
    return
  }

  try {
    await store.deleteTemplate(template.id)
    await loadTemplates()
  } catch (err: any) {
    alert(`Failed to delete template: ${err.message}`)
  }
}

function handleCloseForm() {
  showForm.value = false
  editingTemplate.value = null
}

async function handleFormSuccess() {
  console.log('handleFormSuccess called, closing modal and reloading templates');
  showForm.value = false
  editingTemplate.value = null
  await loadTemplates()
  console.log('Templates reloaded, current count:', templates.value.length);
}

function formatDate(date: string): string {
  return new Date(date).toLocaleDateString()
}

function truncateContent(content: string): string {
  const maxLength = 200
  return content.length > maxLength ? content.substring(0, maxLength) + '...' : content
}
</script>

<style scoped>
.templates-list {
  margin-top: 1rem;
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
  width: 2.5rem;
  height: 2.5rem;
  animation: spin 1s linear infinite;
  margin: 0 auto;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.templates-cards {
  display: grid;
  gap: 1rem;
}

.template-card {
  border: 1px solid #e5e7eb;
  border-radius: 0.5rem;
  overflow: hidden;
  background: white;
  transition: box-shadow 0.2s;
}

.template-card:hover {
  box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: start;
  padding: 1rem;
  background: #f9fafb;
  border-bottom: 1px solid #e5e7eb;
}

.card-title {
  font-weight: 600;
  font-size: 1rem;
  margin-bottom: 0.25rem;
}

.card-subtitle {
  font-size: 0.75rem;
  color: #6b7280;
}

.active-badge {
  display: inline-block;
  padding: 0.125rem 0.5rem;
  border-radius: 0.25rem;
  background: #dcfce7;
  color: #166534;
  font-size: 0.75rem;
  font-weight: 600;
  margin-left: 0.5rem;
}

.card-actions {
  display: flex;
  gap: 0.5rem;
}

.action-btn {
  padding: 0.5rem;
  border: none;
  background: transparent;
  border-radius: 0.375rem;
  cursor: pointer;
  color: #6b7280;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.action-btn:hover {
  background: #f3f4f6;
  color: #1f2937;
}

.action-btn svg {
  width: 18px;
  height: 18px;
}

.card-content {
  padding: 1rem;
}

.template-preview {
  font-size: 0.75rem;
  color: #6b7280;
  white-space: pre-wrap;
  word-wrap: break-word;
  margin: 0;
  font-family: 'Courier New', monospace;
  max-height: 10rem;
  overflow: hidden;
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}

.modal-content {
  background: white;
  border-radius: 0.5rem;
  max-width: 70rem;
  width: 100%;
  max-height: 90vh;
  overflow: auto;
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
  border-radius: 0.375rem;
  border: none;
  background: transparent;
  cursor: pointer;
  color: #6b7280;
  transition: all 0.2s;
}

.close-btn:hover {
  background: #f3f4f6;
  color: #111827;
}
</style>
