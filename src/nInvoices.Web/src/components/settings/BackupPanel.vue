<template>
  <BasePanel
    :title="t('backup.title')"
    :description="t('backup.description')"
  >
    <div class="transfer">
      <section aria-labelledby="backup-export-title">
        <h3 id="backup-export-title">{{ t('backup.exportTitle') }}</h3>
        <template v-if="!plainExport">
          <BaseField :label="t('backup.passphrase')" for="backupPassphrase" :help="passphraseHelp">
            <input id="backupPassphrase" v-model="exportPassphrase" type="password" class="control" autocomplete="new-password" />
          </BaseField>
          <BaseField :label="t('backup.repeatPassphrase')" for="backupPassphraseRepeat" :error="exportPassphraseError">
            <input id="backupPassphraseRepeat" v-model="exportPassphraseRepeat" type="password" class="control" autocomplete="new-password" />
          </BaseField>
          <p class="note warning">{{ t('backup.keepSafe') }}</p>
        </template>
        <p v-else class="note warning">{{ t('backup.plainWarning') }}</p>
        <label class="toggle" for="backupPlain">
          <input id="backupPlain" v-model="plainExport" type="checkbox" />
          <span>{{ t('backup.plain') }}</span>
        </label>
        <div class="buttons">
          <BaseButton icon="download" variant="primary" :loading="exporting" :disabled="!canExport || importing" @click="handleExport">
            {{ t('backup.download') }}
          </BaseButton>
        </div>
      </section>

      <section aria-labelledby="backup-import-title">
        <h3 id="backup-import-title">{{ t('backup.importTitle') }}</h3>
        <p class="note">{{ t('backup.importNote') }}</p>
        <input
          id="importFile"
          ref="importFileInput"
          type="file"
          accept=".json,application/json"
          class="control file"
          :aria-label="t('backup.file')"
          @change="handleFileSelected"
        />
        <BaseField v-if="importEncrypted" :label="t('backup.importPassphrase')" for="importPassphrase" :error="importPassphraseError">
          <input id="importPassphrase" v-model="importPassphrase" type="password" class="control" autocomplete="off" @keydown.enter="handleImport" />
        </BaseField>
        <div v-if="importFile" class="buttons">
          <BaseButton variant="primary" :loading="importing" :disabled="exporting || (importEncrypted && !importPassphrase)" @click="handleImport">
            {{ t('backup.restore') }}
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
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

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

const passphraseHelp = computed(() => t('backup.passphraseHelp', { min: MIN_PASSPHRASE_LENGTH }))

const exportPassphraseError = computed(() =>
  exportPassphraseRepeat.value && exportPassphrase.value !== exportPassphraseRepeat.value
    ? t('backup.passphrasesDiffer')
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
      t(encrypted ? 'backup.downloadedEncrypted' : 'backup.downloadedPlain', { customers: backup.customers?.length ?? 0, invoices: backup.invoices?.length ?? 0 }),
      false)
  } catch (error) {
    showResult(errorMessage(error, t('backup.createFailed')), true)
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
    showResult(t('backup.notABackup'), true)
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
        importPassphraseError.value = t('backup.wrongPassphrase')
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
      showResult(t('backup.nothingToRestore'), true)
      return
    }

    const ok = await confirm({
      title: t('backup.restoreTitle'),
      message: t('backup.restoreMessage', {
        customers: customerCount,
        invoices: invoiceCount,
        extras: [shared ? t('backup.extraShared') : '', settings ? t('backup.extraSettings') : ''].filter(Boolean).map(extra => `, ${extra}`).join('')
      }),
      confirmLabel: t('backup.restore'),
    })
    if (!ok) return

    // Settings first, then customers: invoices are matched to customers by VAT number
    const parts: string[] = []
    const errors: string[] = []
    if (settings) {
      const result = await importExportApi.importSettings(data)
      parts.push(t('backup.partSettings', { added: result.imported, kept: result.skipped }))
      errors.push(...(result.errors ?? []))
    }
    if (customerCount > 0 || shared) {
      const result = await importExportApi.importCustomers(data)
      parts.push(t('backup.partCustomers', { added: result.imported, kept: result.skipped }))
      errors.push(...(result.errors ?? []))
    }
    if (invoiceCount > 0) {
      const result = await importExportApi.importInvoices(data)
      parts.push(t('backup.partInvoices', { added: result.imported, kept: result.skipped }))
      errors.push(...(result.errors ?? []))
    }

    showResult(t('backup.restoreComplete', { parts: parts.join('; ') }), errors.length > 0, errors)
    resetImport()
  } catch (error) {
    showResult(error instanceof SyntaxError ? t('backup.notABackup') : errorMessage(error, t('backup.restoreFailed')), true)
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
