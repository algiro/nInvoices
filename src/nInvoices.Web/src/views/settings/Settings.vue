<template>
  <div class="settings-page">
    <PageHeader title="Settings" subtitle="Appearance, invoice numbering, calendar, images for templates, Gmail, and backups." />

    <div class="sections">
      <BasePanel title="Appearance" description="Pick a light and a dark theme; the mode decides which one is shown. Saved in this browser.">
        <ThemePicker />
      </BasePanel>

      <InvoiceNumberingPanel />

      <BasePanel title="Calendar" description="How the worked-days calendar lays out weeks.">
        <dl class="facts">
          <div>
            <dt>Weeks start on</dt>
            <dd>{{ firstDayOfWeekName }}</dd>
          </div>
        </dl>
        <p class="note">Set by <code>Invoice.FirstDayOfWeek</code> in the API's appsettings.json (0 = Sunday, 1 = Monday … 6 = Saturday); restart the API after changing it.</p>
      </BasePanel>

      <HolidayCalendarsPanel />

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

      <GmailConnectionPanel />

      <ComplianceSettingsPanel />

      <BasePanel
        title="Backup and transfer"
        description="One file with your customers (rates, taxes, projects, worked days, templates), invoices, images, holiday calendars and settings: keep it as a backup, or restore it on another nInvoices server."
      >
        <div class="transfer">
          <section aria-labelledby="backup-export-title">
            <h3 id="backup-export-title">Download a backup</h3>
            <template v-if="!plainExport">
              <BaseField label="Passphrase" for="backupPassphrase" :help="passphraseHelp">
                <input id="backupPassphrase" v-model="exportPassphrase" type="password" class="control" autocomplete="new-password" />
              </BaseField>
              <BaseField label="Repeat the passphrase" for="backupPassphraseRepeat" :error="exportPassphraseError">
                <input id="backupPassphraseRepeat" v-model="exportPassphraseRepeat" type="password" class="control" autocomplete="new-password" />
              </BaseField>
              <p class="note warning">Keep the passphrase safe: without it nobody can open the backup, and it can't be recovered.</p>
            </template>
            <p v-else class="note warning">Anyone who gets this file can read your customers and invoices.</p>
            <label class="toggle" for="backupPlain">
              <input id="backupPlain" v-model="plainExport" type="checkbox" />
              <span>Download without a passphrase (not recommended)</span>
            </label>
            <div class="buttons">
              <BaseButton icon="download" variant="primary" :loading="exporting" :disabled="!canExport || importing" @click="handleExport">
                Download backup
              </BaseButton>
            </div>
          </section>

          <section aria-labelledby="backup-import-title">
            <h3 id="backup-import-title">Restore a backup</h3>
            <p class="note">Records that already exist (same VAT number, or same invoice number) are skipped, so restoring twice is harmless.</p>
            <input
              id="importFile"
              ref="importFileInput"
              type="file"
              accept=".json,application/json"
              class="control file"
              aria-label="Backup file"
              @change="handleFileSelected"
            />
            <BaseField v-if="importEncrypted" label="Passphrase of this backup" for="importPassphrase" :error="importPassphraseError">
              <input id="importPassphrase" v-model="importPassphrase" type="password" class="control" autocomplete="off" @keydown.enter="handleImport" />
            </BaseField>
            <div v-if="importFile" class="buttons">
              <BaseButton variant="primary" :loading="importing" :disabled="exporting || (importEncrypted && !importPassphrase)" @click="handleImport">
                Restore
              </BaseButton>
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
import { importExportApi, imageAssetsApi } from '@/api'
import type { ImageAssetDto } from '@/api/imageAssets'
import type { DataExport } from '@/api/importExport'
import { decryptBackup, encryptBackup, isEncryptedBackup, MIN_PASSPHRASE_LENGTH, WrongPassphraseError } from '@/utils/backupCrypto'
import { useSettingsStore } from '@/stores/settings'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import ThemePicker from '@/components/settings/ThemePicker.vue'
import InvoiceNumberingPanel from '@/components/settings/InvoiceNumberingPanel.vue'
import GmailConnectionPanel from '@/components/settings/GmailConnectionPanel.vue'
import ComplianceSettingsPanel from '@/components/settings/ComplianceSettingsPanel.vue'
import HolidayCalendarsPanel from '@/components/settings/HolidayCalendarsPanel.vue'

const toast = useToast()
const { confirm } = useConfirm()

const settingsStore = useSettingsStore()

const firstDayOfWeekName = computed(() => {
  const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
  const firstDay = settingsStore.invoiceSettings?.firstDayOfWeek ?? 1
  return dayNames[firstDay] || 'Monday'
})

onMounted(() => {
  settingsStore.fetchInvoiceSettings()
  loadImageAssets()
})

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

// Backup and transfer: one file with customers, shared templates and invoices, optionally
// encrypted in the browser with a passphrase (utils/backupCrypto.ts)
const exporting = ref(false)
const importing = ref(false)
const plainExport = ref(false)
const exportPassphrase = ref('')
const exportPassphraseRepeat = ref('')
const importFile = ref<File | null>(null)
const importFileInput = ref<HTMLInputElement | null>(null)
const importEncrypted = ref(false)
const importPassphrase = ref('')
const importPassphraseError = ref<string | null>(null)
const importExportMessage = ref<string | null>(null)
const importExportError = ref(false)
const importErrors = ref<string[]>([])

const passphraseHelp = `At least ${MIN_PASSPHRASE_LENGTH} characters. The backup is encrypted in this browser: the passphrase never leaves it.`

const exportPassphraseError = computed(() =>
  exportPassphraseRepeat.value && exportPassphrase.value !== exportPassphraseRepeat.value
    ? 'The two passphrases differ.'
    : null)

const canExport = computed(() =>
  plainExport.value ||
  (exportPassphrase.value.length >= MIN_PASSPHRASE_LENGTH && exportPassphrase.value === exportPassphraseRepeat.value))

function downloadFile(content: string, filename: string) {
  const blob = new Blob([content], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

function showResult(message: string, isError: boolean, errors: string[] = []) {
  importExportMessage.value = message
  importExportError.value = isError
  importErrors.value = errors
}

async function handleExport() {
  if (!canExport.value) return
  exporting.value = true
  importExportMessage.value = null
  try {
    const [customers, invoices, settings] = await Promise.all([
      importExportApi.exportCustomers(),
      importExportApi.exportInvoices(),
      importExportApi.exportSettings(),
    ])
    const backup: DataExport = {
      exportVersion: customers.exportVersion,
      exportedAt: new Date().toISOString(),
      customers: customers.customers ?? [],
      sharedTemplates: customers.sharedTemplates,
      invoices: invoices.invoices ?? [],
      settings: settings.settings,
    }
    const json = JSON.stringify(backup, null, 2)
    const date = new Date().toISOString().slice(0, 10)
    const encrypted = !plainExport.value
    if (encrypted) {
      downloadFile(JSON.stringify(await encryptBackup(json, exportPassphrase.value)), `ninvoices-backup-${date}.encrypted.json`)
      exportPassphrase.value = ''
      exportPassphraseRepeat.value = ''
    } else {
      downloadFile(json, `ninvoices-backup-${date}.json`)
    }
    showResult(
      `Backup downloaded: ${backup.customers?.length ?? 0} customer(s) and ${backup.invoices?.length ?? 0} invoice(s), ` +
        (encrypted ? 'encrypted with your passphrase.' : 'not encrypted.'),
      false)
  } catch (error: any) {
    showResult(error.message || 'The backup could not be created.', true)
  } finally {
    exporting.value = false
  }
}

async function handleFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  importFile.value = input.files?.[0] ?? null
  importExportMessage.value = null
  importErrors.value = []
  importPassphrase.value = ''
  importPassphraseError.value = null
  importEncrypted.value = false
  if (!importFile.value) return
  try {
    importEncrypted.value = isEncryptedBackup(JSON.parse(await importFile.value.text()))
  } catch {
    showResult('This file is not an nInvoices backup.', true)
  }
}

function resetImport() {
  importFile.value = null
  importEncrypted.value = false
  importPassphrase.value = ''
  if (importFileInput.value) importFileInput.value.value = ''
}

function hasSharedTemplates(data: DataExport): boolean {
  const shared = data.sharedTemplates as Record<string, unknown[] | null | undefined> | null | undefined
  return !!shared && Object.values(shared).some(list => (list?.length ?? 0) > 0)
}

async function handleImport() {
  if (!importFile.value || importing.value) return
  importing.value = true
  importExportMessage.value = null
  importPassphraseError.value = null

  try {
    let content: unknown = JSON.parse(await importFile.value.text())
    if (isEncryptedBackup(content)) {
      try {
        content = JSON.parse(await decryptBackup(content, importPassphrase.value))
      } catch (error) {
        if (!(error instanceof WrongPassphraseError)) throw error
        importPassphraseError.value = error.message
        return
      }
    }

    // Files from the separate customer and invoice exports work too: each holds one of the two
    const data = content as DataExport
    const customerCount = data.customers?.length ?? 0
    const invoiceCount = data.invoices?.length ?? 0
    const shared = hasSharedTemplates(data)
    const settings = data.settings ?? null
    if (customerCount === 0 && invoiceCount === 0 && !shared && !settings) {
      showResult('There is nothing to restore in this file.', true)
      return
    }

    const ok = await confirm({
      title: 'Restore this backup?',
      message: `It holds ${customerCount} customer(s) and ${invoiceCount} invoice(s)` +
        (shared ? ', shared templates' : '') +
        (settings ? ', your images, holiday calendars and settings' : '') +
        '. They are added to your data; what already exists is kept as it is.',
      confirmLabel: 'Restore',
    })
    if (!ok) return

    // Settings first, then customers: invoices are matched to customers by VAT number
    const parts: string[] = []
    const errors: string[] = []
    if (settings) {
      const result = await importExportApi.importSettings(data)
      parts.push(`settings and images: ${result.imported} added, ${result.skipped} kept as they were`)
      errors.push(...(result.errors ?? []))
    }
    if (customerCount > 0 || shared) {
      const result = await importExportApi.importCustomers(data)
      parts.push(`customers: ${result.imported} added, ${result.skipped} already there`)
      errors.push(...(result.errors ?? []))
    }
    if (invoiceCount > 0) {
      const result = await importExportApi.importInvoices(data)
      parts.push(`invoices: ${result.imported} added, ${result.skipped} already there`)
      errors.push(...(result.errors ?? []))
    }

    showResult(`Restore complete. ${parts.join('; ')}.`, errors.length > 0, errors)
    resetImport()
  } catch (error: any) {
    showResult(error instanceof SyntaxError ? 'This file is not an nInvoices backup.' : error.message || 'Restore failed', true)
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

.transfer .note.warning {
  color: var(--color-warning);
}

.toggle {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: var(--text-md);
  color: var(--color-text-secondary);
  cursor: pointer;
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
