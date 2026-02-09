<template>
  <div class="settings-page">
    <h1 class="text-3xl font-bold mb-8">Settings</h1>

    <div class="settings-sections">
      <!-- Invoice Sequence Management -->
      <section class="settings-card">
        <div class="card-header">
          <h2 class="text-xl font-semibold">Invoice Number Sequence</h2>
          <p class="text-gray-600 text-sm mt-1">
            Manage the global invoice number sequence
          </p>
        </div>

        <div class="card-body">
          <div v-if="sequenceLoading" class="loading-state">
            <div class="spinner"></div>
            <p class="mt-2">Loading...</p>
          </div>

          <div v-else-if="sequenceError" class="error-message">
            {{ sequenceError }}
            <button @click="loadSequence" class="btn-secondary mt-2">Retry</button>
          </div>

          <div v-else class="sequence-content">
            <div class="current-sequence">
              <div class="sequence-label">Current Sequence Number</div>
              <div class="sequence-value">{{ currentSequence }}</div>
              <p class="sequence-hint">
                The next invoice will be numbered: {{ nextInvoiceNumber }}
              </p>
            </div>

            <div class="sequence-actions">
              <div class="input-group">
                <label for="newSequence" class="input-label">
                  Set New Sequence Value
                </label>
                <input
                  id="newSequence"
                  v-model.number="newSequenceValue"
                  type="number"
                  min="1"
                  class="input-field"
                  placeholder="Enter new sequence number"
                />
                <p class="input-hint">
                  ⚠️ Warning: Setting this too low may cause duplicate invoice numbers
                </p>
              </div>

              <div class="button-group">
                <button
                  @click="handleUpdateSequence"
                  :disabled="!isSequenceValid || sequenceUpdating"
                  class="btn-primary"
                >
                  {{ sequenceUpdating ? 'Updating...' : 'Update Sequence' }}
                </button>
                <button
                  @click="handleResetSequence"
                  :disabled="sequenceUpdating"
                  class="btn-danger"
                >
                  Reset to 1
                </button>
              </div>
            </div>
          </div>
        </div>
      </section>

      <!-- Invoice Number Format -->
      <section class="settings-card">
        <div class="card-header">
          <h2 class="text-xl font-semibold">Invoice Number Format</h2>
          <p class="text-gray-600 text-sm mt-1">
            The invoice number format is configured in appsettings.json
          </p>
        </div>

        <div class="card-body">
          <div class="info-box">
            <svg class="info-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <div>
              <p class="font-semibold mb-2">Current Format Pattern:</p>
              <code class="code-block">INV-{YEAR}-{MONTH:00}-{NUMBER:000}</code>
              <p class="mt-3 text-sm">
                To change the format, edit the <code>Invoice.NumberFormat</code> setting in appsettings.json and restart the API.
              </p>
              <details class="mt-3">
                <summary class="cursor-pointer text-blue-600 hover:text-blue-800 font-medium">
                  Available tokens
                </summary>
                <ul class="token-list">
                  <li><code>{YEAR}</code> - Full year (2026)</li>
                  <li><code>{YEAR:yy}</code> - Short year (26)</li>
                  <li><code>{MONTH}</code> - Month (1-12)</li>
                  <li><code>{MONTH:00}</code> - Month with padding (01-12)</li>
                  <li><code>{NUMBER}</code> - Sequence number</li>
                  <li><code>{NUMBER:000}</code> - Sequence with padding</li>
                  <li><code>{CUSTOMER}</code> - Customer fiscal ID</li>
                  <li><code>{CUSTOMER:3}</code> - First 3 chars of fiscal ID</li>
                </ul>
              </details>
            </div>
          </div>
        </div>
      </section>

      <!-- First Day of Week -->
      <section class="settings-card">
        <div class="card-header">
          <h2 class="text-xl font-semibold">Calendar Settings</h2>
          <p class="text-gray-600 text-sm mt-1">
            Configure calendar display preferences
          </p>
        </div>

        <div class="card-body">
          <div class="info-box">
            <svg class="info-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
            </svg>
            <div>
              <p class="font-semibold mb-2">First Day of Week:</p>
              <p class="text-sm mb-3">
                Currently configured to start on: <strong>{{ firstDayOfWeekName }}</strong>
              </p>
              <p class="mt-3 text-sm">
                To change this setting, edit the <code>Invoice.FirstDayOfWeek</code> value in appsettings.json:
              </p>
              <ul class="token-list">
                <li><code>0</code> - Sunday</li>
                <li><code>1</code> - Monday (default)</li>
                <li><code>2</code> - Tuesday</li>
                <li><code>3</code> - Wednesday</li>
                <li><code>4</code> - Thursday</li>
                <li><code>5</code> - Friday</li>
                <li><code>6</code> - Saturday</li>
              </ul>
              <p class="mt-3 text-sm text-gray-600">
                After changing this value, restart the API for it to take effect.
              </p>
            </div>
          </div>
        </div>
      </section>

      <!-- Data Management (Import/Export) -->
      <section class="settings-card">
        <div class="card-header">
          <h2 class="section-heading">Data Management</h2>
          <p class="section-description">
            Import and export data for backup or migration between environments
          </p>
        </div>

        <div class="card-body">
          <div class="data-section">
            <h3 class="subsection-title">Export</h3>
            <p class="subsection-description">Download your data as JSON files</p>
            <div class="button-group">
              <button @click="handleExportCustomers" :disabled="exporting" class="btn-primary">
                {{ exporting === 'customers' ? 'Exporting...' : '⬇ Export Customers' }}
              </button>
              <button @click="handleExportInvoices" :disabled="exporting" class="btn-primary">
                {{ exporting === 'invoices' ? 'Exporting...' : '⬇ Export Invoices' }}
              </button>
            </div>
          </div>

          <div class="data-divider"></div>

          <div class="data-section">
            <h3 class="subsection-title">Import</h3>
            <p class="subsection-description">
              Upload a previously exported JSON file. Existing records (matched by Fiscal ID or Invoice Number) will be skipped.
            </p>
            <div class="import-area">
              <input
                ref="importFileInput"
                type="file"
                accept=".json"
                class="file-input"
                @change="handleFileSelected"
              />
              <div v-if="importFile" class="import-preview">
                <p class="file-name">📄 {{ importFile.name }}</p>
                <div class="button-group">
                  <button @click="handleImport('customers')" :disabled="importing" class="btn-primary">
                    {{ importing === 'customers' ? 'Importing...' : 'Import as Customers' }}
                  </button>
                  <button @click="handleImport('invoices')" :disabled="importing" class="btn-primary">
                    {{ importing === 'invoices' ? 'Importing...' : 'Import as Invoices' }}
                  </button>
                </div>
              </div>
            </div>
          </div>

          <div v-if="importExportMessage" class="import-result" :class="importExportError ? 'result-error' : 'result-success'">
            <p>{{ importExportMessage }}</p>
            <ul v-if="importErrors.length" class="error-list">
              <li v-for="(err, i) in importErrors" :key="i">{{ err }}</li>
            </ul>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { invoicesApi, importExportApi } from '@/api'
import type { DataExport } from '@/api/importExport'
import { useSettingsStore } from '@/stores/settings'

const settingsStore = useSettingsStore()

const currentSequence = ref(1)
const newSequenceValue = ref<number | null>(null)
const sequenceLoading = ref(false)
const sequenceUpdating = ref(false)
const sequenceError = ref<string | null>(null)

const isSequenceValid = computed(() => {
  return newSequenceValue.value !== null && newSequenceValue.value >= 1
})

const nextInvoiceNumber = computed(() => {
  // This is a preview - actual format comes from backend
  const num = (currentSequence.value + 1).toString().padStart(3, '0')
  const month = new Date().getMonth() + 1
  const year = new Date().getFullYear()
  return `INV-${year}-${month.toString().padStart(2, '0')}-${num}`
})

const firstDayOfWeekName = computed(() => {
  const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
  const firstDay = settingsStore.invoiceSettings?.firstDayOfWeek ?? 1
  return dayNames[firstDay] || 'Monday'
})

onMounted(() => {
  loadSequence()
  settingsStore.fetchInvoiceSettings()
})

async function loadSequence() {
  sequenceLoading.value = true
  sequenceError.value = null
  try {
    const result = await invoicesApi.getSequence()
    currentSequence.value = result.currentValue
    newSequenceValue.value = null
  } catch (error: any) {
    sequenceError.value = error.message || 'Failed to load sequence'
  } finally {
    sequenceLoading.value = false
  }
}

async function handleUpdateSequence() {
  if (!isSequenceValid.value || newSequenceValue.value === null) {
    return
  }

  const value = newSequenceValue.value

  if (value < currentSequence.value) {
    const confirmed = confirm(
      `⚠️ Warning: You are setting the sequence to ${value}, which is lower than the current value (${currentSequence.value}).\n\n` +
      `This may cause duplicate invoice numbers!\n\n` +
      `Are you sure you want to continue?`
    )
    if (!confirmed) return
  }

  sequenceUpdating.value = true
  sequenceError.value = null
  try {
    const result = await invoicesApi.setSequence(value)
    currentSequence.value = result.currentValue
    newSequenceValue.value = null
    alert(`✓ Sequence updated successfully to ${result.currentValue}`)
  } catch (error: any) {
    sequenceError.value = error.message || 'Failed to update sequence'
    alert(`Failed to update sequence: ${sequenceError.value}`)
  } finally {
    sequenceUpdating.value = false
  }
}

async function handleResetSequence() {
  const confirmed = confirm(
    '⚠️ Warning: You are about to reset the invoice sequence to 1.\n\n' +
    'This will likely cause duplicate invoice numbers!\n\n' +
    'Are you absolutely sure you want to do this?'
  )
  
  if (!confirmed) return

  sequenceUpdating.value = true
  sequenceError.value = null
  try {
    const result = await invoicesApi.setSequence(1)
    currentSequence.value = result.currentValue
    newSequenceValue.value = null
    alert('✓ Sequence reset to 1')
  } catch (error: any) {
    sequenceError.value = error.message || 'Failed to reset sequence'
    alert(`Failed to reset sequence: ${sequenceError.value}`)
  } finally {
    sequenceUpdating.value = false
  }
}

// Import/Export state
const exporting = ref<string | false>(false)
const importing = ref<string | false>(false)
const importFile = ref<File | null>(null)
const importFileInput = ref<HTMLInputElement | null>(null)
const importExportMessage = ref<string | null>(null)
const importExportError = ref(false)
const importErrors = ref<string[]>([])

function downloadJson(data: DataExport, filename: string) {
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

async function handleExportCustomers() {
  exporting.value = 'customers'
  importExportMessage.value = null
  try {
    const data = await importExportApi.exportCustomers()
    const date = new Date().toISOString().slice(0, 10)
    downloadJson(data, `ninvoices-customers-${date}.json`)
    importExportMessage.value = `Exported ${data.customers?.length ?? 0} customer(s)`
    importExportError.value = false
  } catch (error: any) {
    importExportMessage.value = error.message || 'Export failed'
    importExportError.value = true
  } finally {
    exporting.value = false
  }
}

async function handleExportInvoices() {
  exporting.value = 'invoices'
  importExportMessage.value = null
  try {
    const data = await importExportApi.exportInvoices()
    const date = new Date().toISOString().slice(0, 10)
    downloadJson(data, `ninvoices-invoices-${date}.json`)
    importExportMessage.value = `Exported ${data.invoices?.length ?? 0} invoice(s)`
    importExportError.value = false
  } catch (error: any) {
    importExportMessage.value = error.message || 'Export failed'
    importExportError.value = true
  } finally {
    exporting.value = false
  }
}

function handleFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  importFile.value = input.files?.[0] ?? null
  importExportMessage.value = null
  importErrors.value = []
}

async function handleImport(type: 'customers' | 'invoices') {
  if (!importFile.value) return
  importing.value = type
  importExportMessage.value = null
  importErrors.value = []

  try {
    const text = await importFile.value.text()
    const data = JSON.parse(text) as DataExport

    const result = type === 'customers'
      ? await importExportApi.importCustomers(data)
      : await importExportApi.importInvoices(data)

    importExportMessage.value = `Import complete: ${result.imported} imported, ${result.skipped} skipped`
    importErrors.value = result.errors ?? []
    importExportError.value = importErrors.value.length > 0
    importFile.value = null
    if (importFileInput.value) importFileInput.value.value = ''
  } catch (error: any) {
    importExportMessage.value = error.message || 'Import failed'
    importExportError.value = true
  } finally {
    importing.value = false
  }
}
</script>

<style scoped>
.settings-page {
  padding: 2rem;
  max-width: 1200px;
  margin: 0 auto;
}

.settings-sections {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.settings-card {
  background: white;
  border-radius: 0.75rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  overflow: hidden;
}

.card-header {
  padding: 1.5rem;
  border-bottom: 1px solid #e5e7eb;
}

.card-body {
  padding: 1.5rem;
}

.loading-state {
  text-align: center;
  padding: 2rem;
  color: #6b7280;
}

.spinner {
  border: 3px solid #f3f4f6;
  border-top: 3px solid #2563eb;
  border-radius: 50%;
  width: 32px;
  height: 32px;
  animation: spin 1s linear infinite;
  margin: 0 auto;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.error-message {
  padding: 1rem;
  background: #fee2e2;
  color: #991b1b;
  border-radius: 0.5rem;
}

.sequence-content {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.current-sequence {
  padding: 1.5rem;
  background: #f9fafb;
  border-radius: 0.5rem;
  text-align: center;
}

.sequence-label {
  font-size: 0.875rem;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin-bottom: 0.5rem;
}

.sequence-value {
  font-size: 3rem;
  font-weight: 700;
  color: #2563eb;
  margin-bottom: 0.5rem;
}

.sequence-hint {
  font-size: 0.875rem;
  color: #6b7280;
}

.sequence-actions {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.input-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.input-label {
  font-weight: 500;
  color: #374151;
}

.input-field {
  padding: 0.75rem 1rem;
  border: 1px solid #d1d5db;
  border-radius: 0.5rem;
  font-size: 1rem;
  max-width: 300px;
}

.input-field:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.input-hint {
  font-size: 0.875rem;
  color: #f59e0b;
}

.button-group {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
}

.btn-primary,
.btn-secondary,
.btn-danger {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 0.5rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-primary {
  background: #2563eb;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #1d4ed8;
}

.btn-primary:disabled {
  background: #9ca3af;
  cursor: not-allowed;
}

.btn-secondary {
  background: #6b7280;
  color: white;
}

.btn-secondary:hover {
  background: #4b5563;
}

.btn-danger {
  background: #dc2626;
  color: white;
}

.btn-danger:hover:not(:disabled) {
  background: #b91c1c;
}

.btn-danger:disabled {
  background: #9ca3af;
  cursor: not-allowed;
}

.info-box {
  display: flex;
  gap: 1rem;
  padding: 1rem;
  background: #eff6ff;
  border: 1px solid #bfdbfe;
  border-radius: 0.5rem;
}

.info-icon {
  width: 24px;
  height: 24px;
  color: #2563eb;
  flex-shrink: 0;
}

.code-block {
  display: inline-block;
  padding: 0.5rem 1rem;
  background: white;
  border: 1px solid #d1d5db;
  border-radius: 0.375rem;
  font-family: monospace;
  font-size: 0.875rem;
}

.token-list {
  list-style: none;
  padding: 0;
  margin-top: 0.75rem;
}

.token-list li {
  padding: 0.5rem 0;
  border-bottom: 1px solid #e5e7eb;
}

.token-list li:last-child {
  border-bottom: none;
}

.token-list code {
  background: #f3f4f6;
  padding: 0.25rem 0.5rem;
  border-radius: 0.25rem;
  font-family: monospace;
  margin-right: 0.5rem;
}

/* Data Management styles */
.section-heading {
  font-size: 1.25rem;
  font-weight: 600;
}

.section-description {
  color: #6b7280;
  font-size: 0.875rem;
  margin-top: 0.25rem;
}

.data-section {
  margin-bottom: 1rem;
}

.subsection-title {
  font-size: 1rem;
  font-weight: 600;
  margin-bottom: 0.25rem;
}

.subsection-description {
  color: #6b7280;
  font-size: 0.875rem;
  margin-bottom: 0.75rem;
}

.button-group {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.data-divider {
  border-top: 1px solid #e5e7eb;
  margin: 1.5rem 0;
}

.file-input {
  display: block;
  margin-bottom: 0.75rem;
  font-size: 0.875rem;
}

.import-preview {
  margin-top: 0.5rem;
}

.file-name {
  font-size: 0.875rem;
  margin-bottom: 0.5rem;
}

.import-result {
  margin-top: 1rem;
  padding: 0.75rem 1rem;
  border-radius: 0.5rem;
  font-size: 0.875rem;
}

.result-success {
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  color: #166534;
}

.result-error {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #991b1b;
}

.error-list {
  margin-top: 0.5rem;
  padding-left: 1.25rem;
  list-style: disc;
}

.error-list li {
  margin-bottom: 0.25rem;
}
</style>
