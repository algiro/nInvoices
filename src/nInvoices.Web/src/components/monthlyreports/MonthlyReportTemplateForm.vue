<template>
  <div class="template-form">
    <form @submit.prevent="handleSubmit" class="form-content">
      <div class="form-group">
        <label for="name" class="form-label">
          Template Name <span class="text-red-500">*</span>
        </label>
        <input
          id="name"
          v-model="form.name"
          type="text"
          required
          class="form-control"
          placeholder="e.g., Standard Monthly Report"
          :disabled="disabled"
        />
      </div>

      <div class="form-group">
        <div class="flex justify-between items-center mb-2">
          <label for="content" class="form-label mb-0">
            HTML Template <span class="text-red-500">*</span>
          </label>
          <div class="flex gap-2">
            <button
              type="button"
              @click="showSyntaxGuide = !showSyntaxGuide"
              class="text-sm text-blue-600 hover:text-blue-800 hover:underline"
            >
              <svg v-if="showSyntaxGuide" class="w-3 h-3 inline mr-1" fill="currentColor" viewBox="0 0 20 20">
                <path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd" />
              </svg>
              <svg v-else class="w-3 h-3 inline mr-1" fill="currentColor" viewBox="0 0 20 20">
                <path fill-rule="evenodd" d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z" clip-rule="evenodd" />
              </svg>
              <span v-if="showSyntaxGuide">Hide</span>
              <span v-else>Show</span> Syntax Guide
            </button>
            <button
              type="button"
              @click="loadSampleTemplate"
              class="text-sm text-blue-600 hover:text-blue-800 hover:underline"
            >
              <svg class="w-4 h-4 inline mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              Load Sample
            </button>
          </div>
        </div>

        <!-- Syntax Guide -->
        <div v-if="showSyntaxGuide" class="syntax-guide mb-3">
          <h5 class="font-semibold text-sm mb-2">Monthly Report Template Syntax</h5>
          <div class="syntax-examples">
            <div class="syntax-example">
              <strong>Global Variables:</strong>
              <code class="inline-code">[[ year ]]</code>
              <code class="inline-code">[[ month ]]</code>
              <code class="inline-code">[[ monthName ]]</code>
              <code class="inline-code">[[ workedDaysCount ]]</code>
              <code class="inline-code">[[ publicHolidaysCount ]]</code>
              <code class="inline-code">[[ unpaidLeaveCount ]]</code>
            </div>
            <div class="syntax-example">
              <strong>Customer Info:</strong>
              <code class="inline-code">[[ customer.name ]]</code>
              <code class="inline-code">[[ customer.vatNumber ]]</code>
            </div>
            <div class="syntax-example">
              <strong>Day Loop:</strong>
              <code class="inline-code">[[ for day in monthDays ]]...[[ end ]]</code>
            </div>
            <div class="syntax-example">
              <strong>Day Properties:</strong>
              <code class="inline-code">[[ day.date ]]</code>
              <code class="inline-code">[[ day.dayOfWeek ]]</code>
              <code class="inline-code">[[ day.type ]]</code>
              <code class="inline-code">[[ day.isWeekend ]]</code>
              <code class="inline-code">[[ day.isWorked ]]</code>
              <code class="inline-code">[[ day.isPublicHoliday ]]</code>
              <code class="inline-code">[[ day.isUnpaidLeave ]]</code>
            </div>
            <div class="syntax-example">
              <strong>Conditional Styling:</strong>
              <code class="inline-code">[[ if day.isWeekend ]]style="background: #f5f5f5"[[ end ]]</code>
              <code class="inline-code">[[ if day.isWorked ]]style="background: #e8f5e9"[[ end ]]</code>
            </div>
          </div>
        </div>

        <textarea
          id="content"
          v-model="form.content"
          required
          class="form-control code-editor"
          rows="20"
          :disabled="disabled"
          placeholder="Enter your monthly report HTML template..."
        ></textarea>
      </div>

      <div v-if="validationResult" class="validation-result">
        <div v-if="validationResult.isValid" class="validation-success">
          <svg class="w-5 h-5 inline mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          Template is valid
        </div>
        <div v-else class="validation-error">
          <h5 class="font-semibold mb-2">Validation Errors:</h5>
          <ul class="error-list">
            <li v-for="(error, index) in validationResult.errors" :key="index">{{ error }}</li>
          </ul>
        </div>
      </div>

      <div class="form-actions">
        <button
          type="button"
          @click="handleValidate"
          class="btn-secondary"
          :disabled="disabled || !form.content"
        >
          <svg class="w-4 h-4 inline mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          Validate Template
        </button>
        <div class="flex gap-2">
          <button
            type="button"
            @click="handleCancel"
            class="btn-secondary"
            :disabled="disabled"
          >
            Cancel
          </button>
          <button
            type="submit"
            class="btn-primary"
            :disabled="disabled"
          >
            {{ loading ? 'Saving...' : (templateId ? 'Update' : 'Create') }}
          </button>
        </div>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useMonthlyReportTemplatesStore } from '@/stores/monthlyReportTemplates'
import type { CreateMonthlyReportTemplateDto, UpdateMonthlyReportTemplateDto, TemplateValidationResultDto } from '@/types'

const props = defineProps<{
  customerId: number
  templateId?: number
}>()

const emit = defineEmits<{
  success: []
  cancel: []
}>()

const store = useMonthlyReportTemplatesStore()

const form = ref<{ name: string; content: string }>({
  name: '',
  content: ''
})

const showSyntaxGuide = ref(false)
const validationResult = ref<TemplateValidationResultDto | null>(null)
const loading = ref(false)
const disabled = ref(false)

onMounted(async () => {
  if (props.templateId) {
    await loadTemplate()
  }
})

async function loadTemplate() {
  try {
    const template = await store.fetchById(props.templateId!)
    form.value = {
      name: template.name,
      content: template.content
    }
  } catch (err: any) {
    alert(`Failed to load template: ${err.message}`)
  }
}

async function handleValidate() {
  try {
    validationResult.value = await store.validate(form.value.content)
  } catch (err: any) {
    alert(`Validation failed: ${err.message}`)
  }
}

async function handleSubmit() {
  console.log('handleSubmit called', { customerId: props.customerId, name: form.value.name, contentLength: form.value.content.length });
  loading.value = true
  disabled.value = true
  validationResult.value = null

  try {
    if (props.templateId) {
      const dto: UpdateMonthlyReportTemplateDto = {
        name: form.value.name,
        content: form.value.content
      }
      console.log('Updating template', dto);
      await store.update(props.templateId, dto)
    } else {
      const dto: CreateMonthlyReportTemplateDto = {
        customerId: props.customerId,
        name: form.value.name,
        content: form.value.content,
        invoiceType: 0 // InvoiceType.Monthly
      }
      console.log('Creating template', dto);
      await store.create(dto)
    }
    console.log('Success! Emitting success event');
    emit('success')
  } catch (err: any) {
    console.error('Failed to save template:', err);
    alert(`Failed to save template: ${err.message}`)
  } finally {
    loading.value = false
    disabled.value = false
  }
}

function handleCancel() {
  emit('cancel')
}

function loadSampleTemplate() {
  if (form.value.content && !confirm('This will replace your current template. Continue?')) {
    return
  }

  form.value.name = 'Standard Monthly Report'
  form.value.content = `<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Monthly Work Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 2rem; }
        h1 { color: #1e40af; border-bottom: 3px solid #1e40af; padding-bottom: 0.5rem; }
        .summary { margin: 2rem 0; padding: 1rem; background: #f3f4f6; border-radius: 0.5rem; }
        .summary-item { display: inline-block; margin-right: 2rem; }
        .summary-label { font-weight: bold; color: #4b5563; }
        table { width: 100%; border-collapse: collapse; margin-top: 2rem; }
        th { background: #1e40af; color: white; padding: 0.75rem; text-align: left; }
        td { padding: 0.75rem; border: 1px solid #e5e7eb; }
        .worked { background: #e8f5e9; }
        .public-holiday { background: #fff3e0; }
        .unpaid-leave { background: #ffebee; }
        .weekend { background: #f9fafb; color: #9ca3af; }
    </style>
</head>
<body>
    <h1>Monthly Work Report - [[ monthName ]] [[ year ]]</h1>
    
    <div class="summary">
        <div class="summary-item">
            <span class="summary-label">Customer:</span> [[ customer.name ]]
        </div>
        <div class="summary-item">
            <span class="summary-label">Worked Days:</span> [[ workedDaysCount ]]
        </div>
        <div class="summary-item">
            <span class="summary-label">Public Holidays:</span> [[ publicHolidaysCount ]]
        </div>
        <div class="summary-item">
            <span class="summary-label">Unpaid Leave:</span> [[ unpaidLeaveCount ]]
        </div>
    </div>

    <table>
        <thead>
            <tr>
                <th>Date</th>
                <th>Day of Week</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody>
            [[ for day in monthDays ]]
            <tr class="[[ if day.isWeekend ]]weekend[[ else if day.isWorked ]]worked[[ else if day.isPublicHoliday ]]public-holiday[[ else if day.isUnpaidLeave ]]unpaid-leave[[ end ]]">
                <td>[[ day.dateValue | date.to_string "%Y-%m-%d" ]]</td>
                <td>[[ day.dayOfWeek ]]</td>
                <td>[[ day.type ]]</td>
            </tr>
            [[ end ]]
        </tbody>
    </table>
</body>
</html>`
}
</script>

<style scoped>
.template-form {
  padding: 1.5rem;
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
  font-weight: 600;
  margin-bottom: 0.5rem;
  color: #374151;
  font-size: 0.875rem;
}

.form-control {
  padding: 0.625rem 0.75rem;
  border: 1px solid #d1d5db;
  border-radius: 0.375rem;
  font-size: 0.875rem;
  transition: border-color 0.2s;
}

.form-control:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.code-editor {
  font-family: 'Courier New', monospace;
  font-size: 0.813rem;
  line-height: 1.5;
}

.syntax-guide {
  padding: 1rem;
  background: #f9fafb;
  border: 1px solid #e5e7eb;
  border-radius: 0.375rem;
}

.syntax-examples {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.syntax-example {
  font-size: 0.813rem;
}

.syntax-example strong {
  display: block;
  margin-bottom: 0.25rem;
  color: #374151;
}

.inline-code {
  display: inline-block;
  padding: 0.125rem 0.375rem;
  margin: 0.125rem 0.25rem 0.125rem 0;
  background: #e5e7eb;
  border-radius: 0.25rem;
  font-family: 'Courier New', monospace;
  font-size: 0.75rem;
}

.validation-result {
  padding: 1rem;
  border-radius: 0.375rem;
}

.validation-success {
  color: #166534;
  background: #dcfce7;
  border: 1px solid #86efac;
  padding: 0.75rem;
  border-radius: 0.375rem;
}

.validation-error {
  color: #991b1b;
  background: #fee2e2;
  border: 1px solid #fca5a5;
  padding: 0.75rem;
  border-radius: 0.375rem;
}

.error-list {
  list-style: disc;
  padding-left: 1.5rem;
  margin-top: 0.5rem;
}

.form-actions {
  display: flex;
  justify-content: space-between;
  padding-top: 1rem;
  border-top: 1px solid #e5e7eb;
}

.form-hint {
  font-size: 0.75rem;
  color: #6b7280;
  margin-top: 0.25rem;
}
</style>
