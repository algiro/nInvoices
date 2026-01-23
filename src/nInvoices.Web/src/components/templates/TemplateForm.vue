<template>
  <div class="template-form">
    <form @submit.prevent="handleSubmit" class="form-content">
      <div class="form-group">
        <label for="invoiceType" class="form-label">
          Invoice Type <span class="text-red-500">*</span>
        </label>
        <select
          id="invoiceType"
          v-model="form.invoiceType"
          required
          class="form-control"
          :disabled="disabled || !!templateId"
        >
          <option value="Monthly">Monthly Invoice</option>
          <option value="OneTime">One-Time Invoice</option>
        </select>
        <p v-if="templateId" class="form-hint">
          Invoice type cannot be changed after creation
        </p>
      </div>

      <div class="form-group">
        <label for="content" class="form-label">
          Template Content <span class="text-red-500">*</span>
        </label>
        <textarea
          id="content"
          v-model="form.content"
          required
          rows="15"
          placeholder="Enter your template with placeholders like {{InvoiceNumber}}, {{Customer.Name}}, etc."
          class="form-control font-mono text-sm"
          :class="{ 'border-red-500': errors.content }"
          :disabled="disabled"
          @input="handleContentChange"
        ></textarea>
        <p v-if="errors.content" class="text-red-500 text-sm mt-1">{{ errors.content }}</p>
        
        <div class="validation-status" v-if="validationStatus">
          <div v-if="validationStatus.isValid" class="validation-success">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
            </svg>
            <span>Template is valid!</span>
          </div>
          <div v-else class="validation-error">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
            <div>
              <p class="font-semibold">Template has errors:</p>
              <ul class="mt-1 ml-4 list-disc">
                <li v-for="(err, index) in validationStatus.errors" :key="index">{{ err }}</li>
              </ul>
            </div>
          </div>
        </div>
      </div>

      <div class="placeholders-section">
        <h4 class="text-sm font-semibold mb-2">Available Placeholders</h4>
        <div class="placeholders-grid">
          <div class="placeholder-group">
            <p class="placeholder-title">Basic</p>
            <code class="placeholder" v-text="'{{InvoiceNumber}}'"></code>
            <code class="placeholder" v-text="'{{Date}}'"></code>
            <code class="placeholder" v-text="'{{IssueDate}}'"></code>
            <code class="placeholder" v-text="'{{DueDate}}'"></code>
          </div>
          <div class="placeholder-group">
            <p class="placeholder-title">Customer</p>
            <code class="placeholder" v-text="'{{Customer.Name}}'"></code>
            <code class="placeholder" v-text="'{{Customer.FiscalID}}'"></code>
            <code class="placeholder" v-text="'{{Customer.Address.City}}'"></code>
            <code class="placeholder" v-text="'{{Customer.Address.Country}}'"></code>
          </div>
          <div class="placeholder-group">
            <p class="placeholder-title">Amounts</p>
            <code class="placeholder" v-text="'{{Subtotal}}'"></code>
            <code class="placeholder" v-text="'{{TotalTax}}'"></code>
            <code class="placeholder" v-text="'{{Total}}'"></code>
            <code class="placeholder" v-text="'{{Currency}}'"></code>
          </div>
          <div class="placeholder-group" v-if="form.invoiceType === 'Monthly'">
            <p class="placeholder-title">Monthly</p>
            <code class="placeholder" v-text="'{{WorkedDays}}'"></code>
            <code class="placeholder" v-text="'{{MonthNumber}}'"></code>
            <code class="placeholder" v-text="'{{Year}}'"></code>
            <code class="placeholder" v-text="'{{MonthDescription.EN}}'"></code>
          </div>
        </div>
        <p class="form-hint mt-2">
          Click on any placeholder to copy it to clipboard
        </p>
      </div>

      <div class="form-actions">
        <button
          type="button"
          @click="handleValidate"
          class="btn-secondary"
          :disabled="loading || !form.content"
        >
          <svg class="w-4 h-4 inline mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          {{ validating ? 'Validating...' : 'Validate' }}
        </button>
        <button
          type="button"
          @click="handleCancel"
          class="btn-secondary"
          :disabled="loading"
        >
          Cancel
        </button>
        <button
          type="submit"
          class="btn-primary"
          :disabled="loading || disabled || (validationStatus && !validationStatus.isValid)"
        >
          {{ loading ? 'Saving...' : 'Save Template' }}
        </button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useTemplatesStore } from '@/stores/templates'
import type { CreateInvoiceTemplateDto, UpdateInvoiceTemplateDto, InvoiceType, TemplateValidationResultDto } from '@/types'

interface Props {
  customerId: number
  templateId?: number
  disabled?: boolean
}

interface Emits {
  (e: 'success'): void
  (e: 'cancel'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()
const templatesStore = useTemplatesStore()

const loading = ref(false)
const validating = ref(false)
const validationStatus = ref<TemplateValidationResultDto | null>(null)

const form = reactive<CreateInvoiceTemplateDto | UpdateInvoiceTemplateDto>({
  customerId: props.customerId,
  invoiceType: 'Monthly' as InvoiceType,
  content: ''
})

const errors = reactive<Record<string, string>>({})

let validationTimeout: NodeJS.Timeout | null = null

onMounted(async () => {
  if (props.templateId) {
    await loadTemplate(props.templateId)
  }
})

async function loadTemplate(id: number) {
  try {
    loading.value = true
    const template = await templatesStore.fetchById(id)
    if (template) {
      form.invoiceType = template.invoiceType
      form.content = template.content
      await handleValidate()
    }
  } catch (error) {
    console.error('Failed to load template:', error)
  } finally {
    loading.value = false
  }
}

function handleContentChange() {
  validationStatus.value = null
  
  if (validationTimeout) {
    clearTimeout(validationTimeout)
  }
  
  validationTimeout = setTimeout(() => {
    if (form.content.length > 10) {
      handleValidate()
    }
  }, 1000)
}

async function handleValidate() {
  if (!form.content) return

  try {
    validating.value = true
    validationStatus.value = await templatesStore.validateTemplate(form.content)
  } catch (error: any) {
    console.error('Validation failed:', error)
    errors.content = error.message || 'Failed to validate template'
  } finally {
    validating.value = false
  }
}

function validateForm(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])

  if (!form.content.trim()) {
    errors.content = 'Template content is required'
    return false
  }

  if (validationStatus.value && !validationStatus.value.isValid) {
    errors.content = 'Template has validation errors'
    return false
  }

  return true
}

async function handleSubmit() {
  if (!validateForm()) {
    return
  }

  try {
    loading.value = true

    if (props.templateId) {
      await templatesStore.update(props.templateId, form as UpdateInvoiceTemplateDto)
    } else {
      await templatesStore.create(form as CreateInvoiceTemplateDto)
    }

    emit('success')
  } catch (error: any) {
    console.error('Failed to save template:', error)
    errors.content = error.message || 'Failed to save template'
  } finally {
    loading.value = false
  }
}

function handleCancel() {
  emit('cancel')
}
</script>

<style scoped>
.template-form {
  background: white;
  padding: 1.5rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.form-content {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.form-group {
  display: flex;
  flex-direction: column;
}

.form-label {
  font-size: 0.875rem;
  font-weight: 500;
  color: #374151;
  margin-bottom: 0.5rem;
}

.form-hint {
  font-size: 0.75rem;
  color: #6b7280;
  margin-top: 0.25rem;
  font-style: italic;
}

.form-control {
  width: 100%;
  padding: 0.75rem 1rem;
  border: 1px solid #d1d5db;
  border-radius: 0.375rem;
  font-size: 1rem;
  transition: all 0.2s;
}

.form-control:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.form-control:disabled {
  background: #f3f4f6;
  cursor: not-allowed;
}

textarea.form-control {
  resize: vertical;
  line-height: 1.5;
}

.validation-status {
  margin-top: 1rem;
  padding: 1rem;
  border-radius: 0.375rem;
}

.validation-success {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #065f46;
  background: #d1fae5;
  padding: 0.75rem;
  border-radius: 0.375rem;
}

.validation-error {
  display: flex;
  gap: 0.5rem;
  color: #991b1b;
  background: #fee2e2;
  padding: 0.75rem;
  border-radius: 0.375rem;
}

.placeholders-section {
  background: #f9fafb;
  padding: 1rem;
  border-radius: 0.375rem;
  border: 1px solid #e5e7eb;
}

.placeholders-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.placeholder-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.placeholder-title {
  font-size: 0.75rem;
  font-weight: 600;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.placeholder {
  display: inline-block;
  padding: 0.25rem 0.5rem;
  background: white;
  border: 1px solid #d1d5db;
  border-radius: 0.25rem;
  font-size: 0.75rem;
  font-family: 'Courier New', monospace;
  color: #2563eb;
  cursor: pointer;
  transition: all 0.2s;
}

.placeholder:hover {
  background: #eff6ff;
  border-color: #2563eb;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #e5e7eb;
}

.btn-primary,
.btn-secondary {
  padding: 0.75rem 1.5rem;
  border-radius: 0.375rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
}

.btn-primary {
  background: #2563eb;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #1d4ed8;
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: white;
  color: #374151;
  border: 1px solid #d1d5db;
}

.btn-secondary:hover:not(:disabled) {
  background: #f9fafb;
}
</style>