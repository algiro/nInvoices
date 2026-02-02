<template>
  <div class="templates-list">
    <div class="list-header">
      <h3 class="text-lg font-semibold">Invoice Templates</h3>
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
      <p class="text-gray-600 text-sm mb-2">No templates configured yet</p>
      <p class="text-gray-500 text-xs mb-4">Create templates for different invoice types</p>
      <button @click="handleAdd" class="btn-primary">Create First Template</button>
    </div>

    <div v-else class="templates-cards">
      <div v-for="template in templates" :key="template.id" class="template-card">
        <div class="card-header">
          <div>
            <span class="type-badge" :class="getTypeBadgeClass(template.invoiceType)">
              {{ formatType(template.invoiceType) }}
            </span>
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
              @click="handlePreview(template)"
              class="action-btn"
              title="Preview template"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
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
          <TemplateForm
            :customer-id="customerId"
            :template-id="editingTemplate?.id"
            @success="handleFormSuccess"
            @cancel="handleCloseForm"
          />
        </div>
      </div>

      <div v-if="showPreview && previewTemplate" class="modal-overlay" @click="handleClosePreview">
        <div class="modal-content preview-modal" @click.stop>
          <div class="modal-header">
            <h3 class="text-xl font-semibold">Template Preview - {{ formatType(previewTemplate.invoiceType) }}</h3>
            <button @click="handleClosePreview" class="close-btn">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
          <div class="preview-content">
            <pre class="template-full" v-text="previewTemplate.content"></pre>
          </div>
        </div>
      </div>
    </teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useTemplatesStore } from '@/stores/templates'
import TemplateForm from './TemplateForm.vue'
import { InvoiceType, InvoiceTypeNames } from '@/types'
import type { InvoiceTemplateDto } from '@/types'

interface Props {
  customerId: number
}

const props = defineProps<Props>()
const templatesStore = useTemplatesStore()

const showForm = ref(false)
const showPreview = ref(false)
const editingTemplate = ref<InvoiceTemplateDto | null>(null)
const previewTemplate = ref<InvoiceTemplateDto | null>(null)

const templates = computed(() => templatesStore.templatesByCustomer(props.customerId))
const loading = computed(() => templatesStore.loading)
const error = computed(() => templatesStore.error)

onMounted(() => {
  loadTemplates()
})

async function loadTemplates() {
  try {
    await templatesStore.fetchByCustomerId(props.customerId)
  } catch (error) {
    console.error('Failed to load templates:', error)
  }
}

function formatType(type: InvoiceType): string {
  return InvoiceTypeNames[type] || 'Unknown'
}

function getTypeBadgeClass(type: InvoiceType | undefined | null): string {
  if (type === undefined || type === null) {
    return 'type-unknown'
  }
  const name = InvoiceTypeNames[type]?.toLowerCase() || 'unknown'
  const sanitized = name.replace(/[^a-z0-9]/g, '')
  return `type-${sanitized}`
}

function getTypeCssClass(type: InvoiceType): string {
  const name = InvoiceTypeNames[type]?.toLowerCase() || 'unknown'
  return name.replace(/[^a-z0-9]/g, '')
}

function formatDate(date: string): string {
  return new Date(date).toLocaleDateString()
}

function truncateContent(content: string): string {
  const maxLength = 200
  if (content.length <= maxLength) {
    return content
  }
  return content.substring(0, maxLength) + '...'
}

function handleAdd() {
  editingTemplate.value = null
  showForm.value = true
}

function handleEdit(template: InvoiceTemplateDto) {
  editingTemplate.value = template
  showForm.value = true
}

function handlePreview(template: InvoiceTemplateDto) {
  previewTemplate.value = template
  showPreview.value = true
}

async function handleDelete(template: InvoiceTemplateDto) {
  if (!confirm(`Are you sure you want to delete this ${formatType(template.invoiceType)} template?\n\nThis action cannot be undone.`)) {
    return
  }

  try {
    await templatesStore.remove(template.id)
  } catch (error: any) {
    alert(`Failed to delete template: ${error.message}`)
  }
}

async function handleActivate(template: InvoiceTemplateDto) {
  try {
    await templatesStore.activate(template.id)
    await loadTemplates()
  } catch (error: any) {
    alert(`Failed to activate template: ${error.message}`)
  }
}

async function handleDeactivate(template: InvoiceTemplateDto) {
  try {
    await templatesStore.deactivate(template.id)
    await loadTemplates()
  } catch (error: any) {
    alert(`Failed to deactivate template: ${error.message}`)
  }
}

function handleFormSuccess() {
  showForm.value = false
  editingTemplate.value = null
  loadTemplates()
}

function handleCloseForm() {
  showForm.value = false
  editingTemplate.value = null
}

function handleClosePreview() {
  showPreview.value = false
  previewTemplate.value = null
}
</script>

<style scoped>
.templates-list {
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

.templates-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
  gap: 1rem;
}

.template-card {
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 0.5rem;
  overflow: hidden;
  transition: all 0.2s;
}

.template-card:hover {
  border-color: #2563eb;
  box-shadow: 0 2px 8px rgba(37, 99, 235, 0.1);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: start;
  padding: 1rem;
  background: #f9fafb;
  border-bottom: 1px solid #e5e7eb;
}

.type-badge {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.type-monthly {
  background: #dbeafe;
  color: #1e40af;
}

.type-onetime {
  background: #e0e7ff;
  color: #4338ca;
}

.card-subtitle {
  font-size: 0.75rem;
  color: #6b7280;
  margin-top: 0.25rem;
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
  font-family: 'Courier New', monospace;
  font-size: 0.75rem;
  color: #4b5563;
  line-height: 1.5;
  white-space: pre-wrap;
  overflow: hidden;
}

.template-full {
  font-family: 'Courier New', monospace;
  font-size: 0.875rem;
  color: #1f2937;
  line-height: 1.6;
  white-space: pre-wrap;
  padding: 1.5rem;
  background: #f9fafb;
  border-radius: 0.375rem;
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
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: white;
  border-radius: 0.5rem;
  max-width: 800px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
}

.preview-modal {
  max-width: 900px;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  border-bottom: 1px solid #e5e7eb;
  position: sticky;
  top: 0;
  background: white;
  z-index: 10;
}

.preview-content {
  padding: 1.5rem;
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