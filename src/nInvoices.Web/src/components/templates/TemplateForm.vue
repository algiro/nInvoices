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
          placeholder="e.g., My Monthly Invoice Template"
          class="form-control"
          :disabled="disabled"
        />
        <p class="form-hint">
          A descriptive name to identify this template
        </p>
      </div>

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

        <!-- Syntax Guide Collapsible -->
        <div v-if="showSyntaxGuide" class="syntax-guide mb-3">
          <h5 class="font-semibold text-sm mb-2">Scriban Template Syntax</h5>
          <div class="syntax-examples">
            <div class="syntax-example">
              <strong>Placeholders:</strong>
              <code class="inline-code" v-text="'[[ invoiceNumber ]]'"></code>
              <code class="inline-code" v-text="'[[ customer.name ]]'"></code>
            </div>
            <div class="syntax-example">
              <strong>Custom Functions:</strong>
              <code class="inline-code" v-text="'[[ FormatCurrency total currency ]]'"></code>
              <code class="inline-code">[[ FormatDate date "yyyy-MM-dd" ]]</code>
            </div>
            <div class="syntax-example">
              <strong>Loops:</strong>
              <code class="inline-code" v-text="'[[ for item in lineItems ]]...[[ end ]]'"></code>
            </div>
            <div class="syntax-example">
              <strong>Conditionals:</strong>
              <code class="inline-code" v-text="'[[ if workedDays ]]...[[ end ]]'"></code>
            </div>
          </div>
        </div>

        <textarea
          id="content"
          v-model="form.content"
          required
          rows="20"
          placeholder="Enter HTML template with Scriban syntax (see guide above for examples)"
          class="form-control font-mono text-sm"
          :class="{ 'border-red-500': errors.content }"
          :disabled="disabled"
          @input="handleContentChange"
        ></textarea>
        <p v-if="errors.content" class="text-red-500 text-sm mt-1">{{ errors.content }}</p>

        <div class="validation-status" v-if="validationStatus">
          <div v-if="validationStatus.isValid" class="validation-success">
            <svg class="w-5 h-5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
            </svg>
            <div>
              <span class="font-semibold">Template is valid!</span>
              <p class="text-sm mt-1">Found {{ validationStatus.placeholders?.length || 0 }} placeholders</p>
            </div>
          </div>
          <div v-else class="validation-error">
            <svg class="w-5 h-5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
            <div>
              <p class="font-semibold">Template has errors:</p>
              <ul class="mt-1 ml-4 list-disc text-sm">
                <li v-for="(err, index) in validationStatus.errors" :key="index">{{ err }}</li>
              </ul>
            </div>
          </div>
        </div>
      </div>

      <div class="placeholders-section">
        <h4 class="text-sm font-semibold mb-3 flex items-center justify-between">
          <span>Available Placeholders</span>
          <span class="text-xs font-normal text-gray-500">Click to insert at cursor</span>
        </h4>
        <div class="placeholders-grid">
          <div class="placeholder-group">
            <p class="placeholder-title">Invoice Info</p>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('invoiceNumber'))" title="Insert invoice number" v-text="'[[ invoiceNumber ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('invoiceType'))" title="Insert invoice type" v-text="'[[ invoiceType ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('date'))" title="Insert date" v-text="'[[ date ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('dueDate'))" title="Insert due date" v-text="'[[ dueDate ]]'"></code>
          </div>
          <div class="placeholder-group">
            <p class="placeholder-title">Customer</p>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('customerName'))" title="Insert customer name" v-text="'[[ customer.name ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('customerFiscal'))" title="Insert fiscal ID" v-text="'[[ customer.fiscalId ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('customerStreet'))" title="Insert street" v-text="'[[ customer.address.street ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('customerCity'))" title="Insert city" v-text="'[[ customer.address.city ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('customerPostal'))" title="Insert postal code" v-text="'[[ customer.address.postalCode ]]'"></code>
          </div>
          <div class="placeholder-group">
            <p class="placeholder-title">Amounts</p>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('subtotal'))" title="Insert subtotal" v-text="'[[ subtotal ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('totalTax'))" title="Insert total tax" v-text="'[[ totalTax ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('total'))" title="Insert total" v-text="'[[ total ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('currency'))" title="Insert currency" v-text="'[[ currency ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('formatCurrency'))" title="Format with currency symbol" v-text="'[[ FormatCurrency total currency ]]'"></code>
          </div>
          <div class="placeholder-group" v-if="form.invoiceType === 'Monthly'">
            <p class="placeholder-title">Monthly Invoice</p>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('workedDays'))" title="Insert worked days" v-text="'[[ workedDays ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('monthNumber'))" title="Insert month number" v-text="'[[ monthNumber ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('monthDesc'))" title="Insert month name" v-text="'[[ monthDescription ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('monthlyRate'))" title="Insert monthly rate" v-text="'[[ monthlyRate ]]'"></code>
          </div>
          <div class="placeholder-group">
            <p class="placeholder-title">Loops &amp; Functions</p>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('forItems'))" title="Loop over line items" v-text="'[[ for item in lineItems ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('forTaxes'))" title="Loop over taxes" v-text="'[[ for tax in taxes ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('ifWorked'))" title="Conditional block" v-text="'[[ if ... ]]'"></code>
            <code class="placeholder" @click="insertPlaceholder(getPlaceholder('formatDate'))" title="Format date" v-text="'[[ FormatDate ... ]]'"></code>
          </div>
        </div>
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
          {{ validating ? 'Validating...' : 'Validate Template' }}
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
const showSyntaxGuide = ref(false)
const validationStatus = ref<TemplateValidationResultDto | null>(null)
const contentTextarea = ref<HTMLTextAreaElement | null>(null)

const form = reactive<CreateInvoiceTemplateDto | UpdateInvoiceTemplateDto>({
  customerId: props.customerId,
  invoiceType: 'Monthly' as InvoiceType,
  name: '',
  content: ''
})

const errors = reactive<Record<string, string>>({})

let validationTimeout: NodeJS.Timeout | null = null

// Function to get placeholder text - uses [[ ]] delimiters (Vue-safe)
function getPlaceholder(name: string): string {
  const placeholders: Record<string, string> = {
    invoiceNumber: '[[ invoiceNumber ]]',
    invoiceType: '[[ invoiceType ]]',
    date: '[[ date ]]',
    dueDate: '[[ dueDate ]]',
    customerName: '[[ customer.name ]]',
    customerFiscal: '[[ customer.fiscalId ]]',
    customerStreet: '[[ customer.address.street ]]',
    customerCity: '[[ customer.address.city ]]',
    customerPostal: '[[ customer.address.postalCode ]]',
    subtotal: '[[ subtotal ]]',
    totalTax: '[[ totalTax ]]',
    total: '[[ total ]]',
    currency: '[[ currency ]]',
    formatCurrency: '[[ FormatCurrency total currency ]]',
    workedDays: '[[ workedDays ]]',
    monthNumber: '[[ monthNumber ]]',
    monthDesc: '[[ monthDescription ]]',
    monthlyRate: '[[ monthlyRate ]]',
    forItems: '[[ for item in lineItems ]][[ end ]]',
    forTaxes: '[[ for tax in taxes ]][[ end ]]',
    ifWorked: '[[ if workedDays ]][[ end ]]',
    formatDate: '[[ FormatDate date "MMMM dd, yyyy" ]]'
  }
  return placeholders[name] || ''
}

onMounted(async () => {
  if (props.templateId) {
    await loadTemplate(props.templateId)
  }
  
  // Get reference to textarea for insertPlaceholder
  contentTextarea.value = document.getElementById('content') as HTMLTextAreaElement
})

async function loadTemplate(id: number) {
  try {
    loading.value = true
    const template = await templatesStore.fetchById(id)
    if (template) {
      form.invoiceType = template.invoiceType
      form.name = template.name
      form.content = template.content
      await handleValidate()
    }
  } catch (error) {
    console.error('Failed to load template:', error)
  } finally {
    loading.value = false
  }
}

function loadSampleTemplate() {
  const sample = `<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
</head>
<body>
    <h1>INVOICE</h1>
    
    <h3>Invoice #[[ invoiceNumber ]]</h3>
    <p>Date: [[ date ]]</p>
    
    <h3>Bill To:</h3>
    <p>
        <strong>[[ customer.name ]]</strong><br>
        [[ customer.address.street ]]<br>
        [[ customer.address.city ]], [[ customer.address.postalCode ]]<br>
        VAT: [[ customer.fiscalId ]]
    </p>
    
    <h3>Services &amp; Charges</h3>
    <table>
        <tr>
            <th width="60%">Description</th>
            <th width="20%">Quantity</th>
            <th width="20%">Amount</th>
        </tr>
        
        [[ for item in lineItems ]]
        <tr>
            <td>[[ item.description ]]</td>
            <td>[[ item.quantity ]]</td>
            <td>[[ FormatCurrency item.amount currency ]]</td>
        </tr>
        [[ end ]]
        
        <tr>
            <td><strong>Subtotal</strong></td>
            <td></td>
            <td><strong>[[ FormatCurrency subtotal currency ]]</strong></td>
        </tr>
        
        [[ for tax in taxes ]]
        <tr>
            <td>[[ tax.description ]] ([[ FormatDecimal tax.rate 2 ]]%)</td>
            <td></td>
            <td>[[ FormatCurrency tax.amount currency ]]</td>
        </tr>
        [[ end ]]
        
        <tr>
            <td><h3>TOTAL</h3></td>
            <td></td>
            <td><h3>[[ FormatCurrency total currency ]]</h3></td>
        </tr>
    </table>
    
    [[ if workedDays ]]
    <p>
        <strong>Period:</strong> [[ monthDescription ]]<br>
        <strong>Days Worked:</strong> [[ workedDays ]]<br>
        <strong>Daily Rate:</strong> [[ FormatCurrency monthlyRate currency ]]
    </p>
    [[ end ]]
    
    <p>Thank you for your business!</p>
</body>
</html>`
  
  if (form.content && !confirm('This will replace your current template. Continue?')) {
    return
  }
  
  form.content = sample
  handleValidate()
}

function insertPlaceholder(placeholder: string) {
  const textarea = contentTextarea.value
  if (!textarea) return

  const start = textarea.selectionStart
  const end = textarea.selectionEnd
  const text = form.content

  // Insert placeholder at cursor position
  form.content = text.substring(0, start) + placeholder + text.substring(end)

  // Restore focus and move cursor
  setTimeout(() => {
    textarea.focus()
    const newPosition = start + placeholder.length
    textarea.setSelectionRange(newPosition, newPosition)
  }, 0)
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
  }, 1500) // Increased debounce time
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
    errors.content = 'Template has validation errors. Please fix them before saving.'
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

.syntax-guide {
  background: #eff6ff;
  border: 1px solid #bfdbfe;
  border-radius: 0.375rem;
  padding: 1rem;
}

.syntax-examples {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.syntax-example {
  font-size: 0.875rem;
}

.syntax-example strong {
  display: block;
  margin-bottom: 0.25rem;
  color: #1e40af;
}

.inline-code {
  display: inline-block;
  padding: 0.125rem 0.375rem;
  background: white;
  border: 1px solid #bfdbfe;
  border-radius: 0.25rem;
  font-family: 'Courier New', monospace;
  font-size: 0.75rem;
  margin-right: 0.5rem;
  margin-bottom: 0.25rem;
}

.validation-status {
  margin-top: 1rem;
}

.validation-success {
  display: flex;
  align-items: start;
  gap: 0.75rem;
  color: #065f46;
  background: #d1fae5;
  padding: 0.75rem;
  border-radius: 0.375rem;
  border: 1px solid #6ee7b7;
}

.validation-error {
  display: flex;
  align-items: start;
  gap: 0.75rem;
  color: #991b1b;
  background: #fee2e2;
  padding: 0.75rem;
  border-radius: 0.375rem;
  border: 1px solid #fca5a5;
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
  margin-bottom: 0.25rem;
}

.placeholder {
  display: inline-block;
  padding: 0.375rem 0.625rem;
  background: white;
  border: 1px solid #d1d5db;
  border-radius: 0.25rem;
  font-size: 0.75rem;
  font-family: 'Courier New', monospace;
  color: #2563eb;
  cursor: pointer;
  transition: all 0.2s;
  text-align: left;
}

.placeholder:hover {
  background: #eff6ff;
  border-color: #2563eb;
  transform: translateY(-1px);
  box-shadow: 0 2px 4px rgba(37, 99, 235, 0.1);
}

.placeholder:active {
  transform: translateY(0);
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
  font-size: 0.875rem;
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

.btn-secondary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.flex {
  display: flex;
}

.justify-between {
  justify-content: space-between;
}

.items-center {
  align-items: center;
}

.gap-2 {
  gap: 0.5rem;
}

.mb-0 {
  margin-bottom: 0;
}

.mb-2 {
  margin-bottom: 0.5rem;
}

.mb-3 {
  margin-bottom: 0.75rem;
}
</style>
