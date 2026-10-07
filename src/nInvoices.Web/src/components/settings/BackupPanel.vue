<template>
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
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { importExportApi } from '@/api'
import type { DataExport } from '@/api/importExport'
import { decryptBackup, encryptBackup, isEncryptedBackup, MIN_PASSPHRASE_LENGTH, WrongPassphraseError } from '@/utils/backupCrypto'
import { errorMessage } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

const { confirm } = useConfirm()

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
  } catch (error) {
    showResult(errorMessage(error, 'The backup could not be created.'), true)
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
  } catch (error) {
    showResult(error instanceof SyntaxError ? 'This file is not an nInvoices backup.' : errorMessage(error, 'Restore failed'), true)
  } finally {
    importing.value = false
  }
}
</script>

<style scoped>
.note {
  margin: 0.75rem 0 0;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.control.file {
  padding: 0.35rem 0.5rem;
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
