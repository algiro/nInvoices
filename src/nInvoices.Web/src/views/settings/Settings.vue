<template>
  <div class="settings-page">
    <PageHeader title="Settings" subtitle="Invoice numbering, images for templates, and backups." />

    <div class="sections">
      <BasePanel title="Invoice numbering" description="Every new invoice takes the next number in the sequence, formatted with the pattern below.">
        <LoadingState v-if="sequenceLoading" label="Loading the sequence…" />

        <EmptyState v-else-if="sequenceError && !sequenceLoaded" icon="alert" title="The sequence could not be loaded" :description="sequenceError" compact>
          <BaseButton @click="loadSequence">Try again</BaseButton>
        </EmptyState>

        <div v-else class="numbering">
          <div class="next-number">
            <span class="label">Next invoice number</span>
            <span class="value mono">{{ nextInvoiceNumber }}</span>
            <span class="hint">Sequence value {{ currentSequence }} · pattern <code>{{ numberFormat }}</code></span>
          </div>

          <form class="sequence-form" novalidate @submit.prevent="handleUpdateSequence">
            <BaseField
              label="Change the next sequence value"
              for="newSequence"
              :help="newSequenceValue && newSequenceValue < currentSequence ? 'Lower than the current value: numbers already used may be issued again.' : 'Use this after importing invoices or when moving from another tool.'"
            >
              <div class="inline">
                <input
                  id="newSequence"
                  v-model.number="newSequenceValue"
                  type="number"
                  min="1"
                  class="control num"
                  :placeholder="String(currentSequence)"
                />
                <BaseButton type="submit" variant="primary" :loading="sequenceUpdating" :disabled="!isSequenceValid">Save</BaseButton>
              </div>
            </BaseField>
            <BaseButton variant="ghost-danger" :disabled="sequenceUpdating" @click="handleResetSequence">Reset to 1…</BaseButton>
          </form>
        </div>

        <details class="tokens">
          <summary>How the pattern works</summary>
          <p>The pattern is set in <code>Invoice.NumberFormat</code> in the API's appsettings.json; restart the API after changing it.</p>
          <table class="data-table compact">
            <thead><tr><th scope="col">Token</th><th scope="col">Becomes</th><th scope="col">Example</th></tr></thead>
            <tbody>
              <tr v-for="token in tokens" :key="token.code">
                <td><code>{{ token.code }}</code></td>
                <td>{{ token.meaning }}</td>
                <td class="mono">{{ token.example }}</td>
              </tr>
            </tbody>
          </table>
        </details>
      </BasePanel>

      <BasePanel title="Calendar" description="How the worked-days calendar lays out weeks.">
        <dl class="facts">
          <div>
            <dt>Weeks start on</dt>
            <dd>{{ firstDayOfWeekName }}</dd>
          </div>
        </dl>
        <p class="note">Set by <code>Invoice.FirstDayOfWeek</code> in the API's appsettings.json (0 = Sunday, 1 = Monday … 6 = Saturday); restart the API after changing it.</p>
      </BasePanel>

      <BasePanel title="Images for templates" description="Logos and signatures you can place in invoice and timesheet templates.">
        <form class="upload" novalidate @submit.prevent="handleUploadImage">
          <BaseField label="Name to use in templates" for="imageAlias" help="Letters and numbers, e.g. companyLogo.">
            <input id="imageAlias" v-model="imageAlias" type="text" class="control" placeholder="companyLogo" />
          </BaseField>
          <BaseField label="Image" for="imageFile" help="PNG, JPEG, GIF, SVG or WebP, up to 1 MB." :error="imageUploadError">
            <input
              id="imageFile"
              ref="imageFileInput"
              type="file"
              accept="image/png,image/jpeg,image/gif,image/svg+xml,image/webp"
              class="control file"
              @change="handleImageFileSelected"
            />
          </BaseField>
          <BaseButton type="submit" variant="primary" icon="plus" :loading="imageUploading" :disabled="!canUpload" class="upload-btn">Upload</BaseButton>
        </form>

        <LoadingState v-if="imageAssetsLoading" label="Loading images…" />
        <EmptyState
          v-else-if="imageAssets.length === 0"
          icon="template"
          title="No images yet"
          description="Upload a logo, then add it to a template with the snippet shown on its card."
          compact
        />
        <ul v-else class="images">
          <li v-for="asset in imageAssets" :key="asset.id" class="image-card">
            <div class="thumb">
              <img
                v-if="imageDataCache[asset.id]"
                :src="`data:${asset.contentType};base64,${imageDataCache[asset.id]}`"
                :alt="asset.alias"
              />
              <button v-else type="button" class="load-thumb" @click="loadImageData(asset.id)">Show preview</button>
            </div>
            <div class="image-body">
              <strong>{{ asset.alias }}</strong>
              <span class="muted">{{ asset.fileName }} · {{ formatFileSize(asset.fileSize) }}</span>
              <div class="snippet">
                <code>[[ Image "{{ asset.alias }}" ]]</code>
                <BaseButton size="sm" variant="ghost" @click="copySnippet(asset.alias)">Copy</BaseButton>
              </div>
            </div>
            <BaseButton
              size="sm"
              variant="ghost-danger"
              icon="trash"
              icon-only
              class="image-delete"
              :aria-label="`Delete ${asset.alias}`"
              title="Delete"
              @click="handleDeleteImage(asset)"
            />
          </li>
        </ul>
      </BasePanel>

      <BasePanel title="Backup and transfer" description="Export customers or invoices as JSON, for backups or to move them to another installation.">
        <div class="transfer">
          <section>
            <h3>Export</h3>
            <div class="buttons">
              <BaseButton icon="download" :loading="exporting === 'customers'" :disabled="!!exporting" @click="handleExportCustomers">Customers</BaseButton>
              <BaseButton icon="download" :loading="exporting === 'invoices'" :disabled="!!exporting" @click="handleExportInvoices">Invoices</BaseButton>
            </div>
          </section>

          <section>
            <h3>Import</h3>
            <p class="note">Records that already exist (same VAT number, or same invoice number) are skipped.</p>
            <input
              id="importFile"
              ref="importFileInput"
              type="file"
              accept=".json"
              class="control file"
              aria-label="Exported JSON file"
              @change="handleFileSelected"
            />
            <div v-if="importFile" class="buttons">
              <BaseButton variant="primary" :loading="importing === 'customers'" :disabled="!!importing" @click="handleImport('customers')">Import customers</BaseButton>
              <BaseButton variant="primary" :loading="importing === 'invoices'" :disabled="!!importing" @click="handleImport('invoices')">Import invoices</BaseButton>
            </div>
          </section>
        </div>

        <div v-if="importExportMessage" class="result" :class="importExportError ? 'error' : 'success'" role="status">
          <p>{{ importExportMessage }}</p>
          <ul v-if="importErrors.length">
            <li v-for="(err, i) in importErrors" :key="i">{{ err }}</li>
          </ul>
        </div>
      </BasePanel>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, reactive } from 'vue'
import { invoicesApi, importExportApi, imageAssetsApi } from '@/api'
import type { ImageAssetDto } from '@/api/imageAssets'
import type { DataExport } from '@/api/importExport'
import { useSettingsStore } from '@/stores/settings'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'

const toast = useToast()
const { confirm } = useConfirm()

const settingsStore = useSettingsStore()

const currentSequence = ref(1)
const newSequenceValue = ref<number | null>(null)
const sequenceLoading = ref(false)
const sequenceUpdating = ref(false)
const sequenceError = ref<string | null>(null)

const isSequenceValid = computed(() => {
  return newSequenceValue.value !== null && newSequenceValue.value >= 1
})

const sequenceLoaded = ref(false)

// The pattern configured in the API (Invoice.NumberFormat)
const numberFormat = computed(() => settingsStore.invoiceSettings?.numberFormat || '{YEAR:yy}-{MONTH:00}-{NUMBER:000}')

/**
 * Mirrors InvoiceNumber.Format in the backend. The stored sequence value is the number the
 * next invoice receives (the backend hands it out, then increments it).
 */
function formatInvoiceNumber(pattern: string, sequence: number, date = new Date(), customerCode = 'NEA'): string {
  return pattern
    .replace(/\{YEAR:yy\}/g, String(date.getFullYear()).slice(-2))
    .replace(/\{YEAR\}/g, String(date.getFullYear()))
    .replace(/\{MONTH:00\}/g, String(date.getMonth() + 1).padStart(2, '0'))
    .replace(/\{MONTH\}/g, String(date.getMonth() + 1))
    .replace(/\{CUSTOMER:3\}/g, customerCode.slice(0, 3).toUpperCase())
    .replace(/\{CUSTOMER\}/g, customerCode.toUpperCase())
    .replace(/\{NUMBER:(0+)\}/g, (_, zeros: string) => String(sequence).padStart(zeros.length, '0'))
    .replace(/\{NUMBER\}/g, String(sequence))
}

const nextInvoiceNumber = computed(() => formatInvoiceNumber(numberFormat.value, currentSequence.value))

const tokens = computed(() => {
  const now = new Date()
  return [
    { code: '{YEAR}', meaning: 'Year of the issue date', example: String(now.getFullYear()) },
    { code: '{YEAR:yy}', meaning: 'Two-digit year', example: String(now.getFullYear()).slice(-2) },
    { code: '{MONTH}', meaning: 'Month number', example: String(now.getMonth() + 1) },
    { code: '{MONTH:00}', meaning: 'Month number, two digits', example: String(now.getMonth() + 1).padStart(2, '0') },
    { code: '{NUMBER}', meaning: 'Sequence value', example: String(currentSequence.value) },
    { code: '{NUMBER:000}', meaning: 'Sequence value padded with zeros (one 0 per digit)', example: String(currentSequence.value).padStart(3, '0') },
    { code: '{CUSTOMER}', meaning: 'Customer VAT number / fiscal ID, upper case', example: 'IT0123…' },
    { code: '{CUSTOMER:3}', meaning: 'First three characters of it', example: 'IT0' }
  ]
})

const firstDayOfWeekName = computed(() => {
  const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
  const firstDay = settingsStore.invoiceSettings?.firstDayOfWeek ?? 1
  return dayNames[firstDay] || 'Monday'
})

onMounted(() => {
  loadSequence()
  settingsStore.fetchInvoiceSettings()
  loadImageAssets()
})

async function loadSequence() {
  sequenceLoading.value = true
  sequenceError.value = null
  try {
    const result = await invoicesApi.getSequence()
    currentSequence.value = result.currentValue
    newSequenceValue.value = null
    sequenceLoaded.value = true
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
    const confirmed = await confirm({
      title: 'Lower the invoice sequence?',
      message: `The sequence goes from ${currentSequence.value} down to ${value}. New invoices may reuse numbers that already exist.`,
      confirmLabel: 'Lower sequence',
      tone: 'danger'
    })
    if (!confirmed) return
  }

  sequenceUpdating.value = true
  sequenceError.value = null
  try {
    const result = await invoicesApi.setSequence(value)
    currentSequence.value = result.currentValue
    newSequenceValue.value = null
    toast.success('Invoice sequence updated', { message: `Next sequence value: ${result.currentValue}` })
  } catch (error: any) {
    sequenceError.value = error.message || 'Failed to update sequence'
    toast.error('Failed to update sequence', { message: sequenceError.value ?? undefined })
  } finally {
    sequenceUpdating.value = false
  }
}

async function handleResetSequence() {
  const confirmed = await confirm({
    title: 'Reset the invoice sequence to 1?',
    message: 'New invoices will very likely reuse numbers that already exist.',
    confirmLabel: 'Reset to 1',
    tone: 'danger'
  })

  if (!confirmed) return

  sequenceUpdating.value = true
  sequenceError.value = null
  try {
    const result = await invoicesApi.setSequence(1)
    currentSequence.value = result.currentValue
    newSequenceValue.value = null
    toast.success('Invoice sequence reset to 1')
  } catch (error: any) {
    sequenceError.value = error.message || 'Failed to reset sequence'
    toast.error('Failed to reset sequence', { message: sequenceError.value ?? undefined })
  } finally {
    sequenceUpdating.value = false
  }
}

// Image Assets state
const imageAssets = ref<ImageAssetDto[]>([])
const imageAssetsLoading = ref(false)
const imageAlias = ref('')
const imageFile = ref<File | null>(null)
const imageFileInput = ref<HTMLInputElement | null>(null)
const imageUploading = ref(false)
const imageUploadError = ref<string | null>(null)
const imageDataCache = reactive<Record<number, string>>({})

const canUpload = computed(() => imageAlias.value.trim() && imageFile.value)

function handleImageFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  imageFile.value = input.files?.[0] ?? null
  imageUploadError.value = null
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1048576).toFixed(1)} MB`
}

async function loadImageAssets() {
  imageAssetsLoading.value = true
  try {
    imageAssets.value = await imageAssetsApi.getAll()
    // Auto-load previews for all images
    for (const asset of imageAssets.value) {
      loadImageData(asset.id)
    }
  } catch (error: any) {
    console.error('Failed to load image assets:', error)
  } finally {
    imageAssetsLoading.value = false
  }
}

async function loadImageData(id: number) {
  if (imageDataCache[id]) return
  try {
    const data = await imageAssetsApi.getById(id)
    imageDataCache[id] = data.base64Data
  } catch (error: any) {
    console.error('Failed to load image data:', error)
  }
}

async function handleUploadImage() {
  if (!imageAlias.value.trim() || !imageFile.value) return
  imageUploading.value = true
  imageUploadError.value = null
  try {
    await imageAssetsApi.upload(imageAlias.value.trim(), imageFile.value)
    imageAlias.value = ''
    imageFile.value = null
    if (imageFileInput.value) imageFileInput.value.value = ''
    await loadImageAssets()
  } catch (error: any) {
    imageUploadError.value = error.response?.data?.error || error.message || 'Upload failed'
  } finally {
    imageUploading.value = false
  }
}

async function copySnippet(alias: string) {
  const snippet = `[[ Image "${alias}" ]]`
  try {
    await navigator.clipboard.writeText(snippet)
    toast.success('Snippet copied', { message: snippet })
  } catch {
    toast.info('Copy this into your template', { message: snippet })
  }
}

async function handleDeleteImage(asset: ImageAssetDto) {
  if (!(await confirm({ title: 'Delete image?', message: `"${asset.alias}" will be deleted. Templates that use it will show a placeholder.`, confirmLabel: 'Delete', tone: 'danger' }))) return
  try {
    await imageAssetsApi.delete(asset.id)
    delete imageDataCache[asset.id]
    await loadImageAssets()
  } catch (error: any) {
    toast.failure('Failed to delete image', error)
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
  max-width: 60rem;
}

.sections {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.mono {
  font-family: var(--font-mono);
}

code {
  padding: 0.05rem 0.3rem;
  border-radius: var(--radius-sm);
  background: var(--color-surface-sunken);
  font-size: 0.85em;
}

.note {
  margin: 0.75rem 0 0;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

/* numbering */
.numbering {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1.2fr);
  gap: 1.25rem;
  align-items: start;
}

@media (max-width: 760px) {
  .numbering {
    grid-template-columns: minmax(0, 1fr);
  }
}

.next-number {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  padding: 1rem 1.1rem;
  border-radius: var(--radius-md);
  background: var(--color-primary-soft);
}

.next-number .label {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.next-number .value {
  font-size: 1.6rem;
  font-weight: 650;
  color: var(--color-primary);
}

.next-number .hint {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.sequence-form {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.6rem;
}

.sequence-form :deep(.field) {
  width: 100%;
}

.inline {
  display: flex;
  gap: 0.5rem;
}

.inline .control {
  max-width: 10rem;
}

.tokens {
  margin-top: 1.25rem;
  font-size: var(--text-md);
}

.tokens summary {
  cursor: pointer;
  color: var(--color-primary);
  font-weight: 500;
}

.tokens p {
  color: var(--color-text-muted);
}

.data-table.compact td,
.data-table.compact th {
  padding: 0.4rem 0.7rem;
}

/* calendar */
.facts {
  margin: 0;
}

.facts div {
  display: flex;
  gap: 1rem;
}

.facts dt {
  color: var(--color-text-muted);
}

.facts dd {
  margin: 0;
  font-weight: 600;
  color: var(--color-text);
}

/* images */
.upload {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr) auto;
  gap: 0.75rem;
  align-items: start;
  margin-bottom: 1.25rem;
}

.upload-btn {
  margin-top: 1.55rem;
}

@media (max-width: 760px) {
  .upload {
    grid-template-columns: minmax(0, 1fr);
  }

  .upload-btn {
    margin-top: 0;
    justify-self: start;
  }
}

.control.file {
  padding: 0.35rem 0.5rem;
}

.images {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(17rem, 1fr));
  gap: 0.75rem;
  margin: 0;
  padding: 0;
  list-style: none;
}

.image-card {
  position: relative;
  display: flex;
  flex-direction: column;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  overflow: hidden;
  background: var(--color-surface);
}

.thumb {
  height: 7.5rem;
  display: grid;
  place-items: center;
  padding: 0.75rem;
  background:
    repeating-conic-gradient(var(--color-surface-sunken) 0% 25%, var(--color-surface) 0% 50%) 0 0 / 16px 16px;
  border-bottom: 1px solid var(--color-border);
}

.thumb img {
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}

.load-thumb {
  border: 0;
  background: transparent;
  color: var(--color-primary);
  font-size: var(--text-sm);
}

.image-body {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  padding: 0.65rem 0.75rem 0.7rem;
  font-size: var(--text-md);
}

.image-body strong {
  color: var(--color-text);
}

.image-body .muted {
  font-size: var(--text-sm);
}

.snippet {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  margin-top: 0.4rem;
}

.snippet code {
  overflow-wrap: anywhere;
}

.image-delete {
  position: absolute;
  top: 0.4rem;
  right: 0.4rem;
  background: var(--color-surface);
}

/* backup */
.transfer {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1.5rem;
}

@media (max-width: 760px) {
  .transfer {
    grid-template-columns: minmax(0, 1fr);
  }
}

.transfer section {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}

.transfer h3 {
  font-size: var(--text-md);
  font-weight: 600;
}

.transfer .note {
  margin: 0;
}

.buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.result {
  margin-top: 1.25rem;
  padding: 0.75rem 0.9rem;
  border: 1px solid;
  border-radius: var(--radius-md);
  font-size: var(--text-md);
}

.result p {
  margin: 0;
  font-weight: 500;
}

.result ul {
  margin: 0.5rem 0 0;
  padding-left: 1.2rem;
}

.result.success {
  border-color: var(--color-success-line);
  background: var(--color-success-soft);
  color: var(--color-success);
}

.result.error {
  border-color: var(--color-danger-line);
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
</style>
