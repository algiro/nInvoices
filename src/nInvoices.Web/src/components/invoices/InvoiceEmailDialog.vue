<template>
  <BaseDialog
    :open="open"
    :title="created ? t('invoiceEmail.draftCreated') : t('invoiceEmail.title', { number: invoice.invoiceNumber })"
    :description="created ? undefined : t('invoiceEmail.description')"
    size="xl"
    :close-on-overlay="false"
    @close="emit('close')"
  >
    <!-- Done: link to the draft -->
    <div v-if="created" class="done">
      <i18n-t keypath="invoiceEmail.done" tag="p">
        <template #to><strong>{{ created.to }}</strong></template>
        <template #from><strong>{{ created.from }}</strong></template>
        <template #attachments>{{ listOf(created.attachments) }}</template>
      </i18n-t>
      <p v-if="canMarkSent" class="muted">{{ t('invoiceEmail.markSentHint') }}</p>
    </div>

    <LoadingState v-else-if="loading" :label="t('invoiceEmail.preparing')" />

    <EmptyState v-else-if="loadError" icon="alert" :title="t('invoiceEmail.prepareFailed')" :description="loadError" compact>
      <BaseButton @click="load()">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <form v-else-if="compose" id="invoice-email-form" class="compose" novalidate @submit.prevent="createDraft">
      <div v-if="!gmailReady" class="notice warning" role="status">
        <AppIcon name="alert" />
        <span>
          {{ notConnectedMessage }}
          <router-link to="/settings/email" @click="emit('close')">{{ t('invoiceEmail.openSettings') }}</router-link>
        </span>
      </div>

      <div v-if="compose.errors.length" class="notice danger" role="alert">
        <AppIcon name="error" />
        <div>
          <strong>{{ t('invoiceEmail.templateProblems') }}</strong>
          <ul>
            <li v-for="(problem, index) in compose.errors" :key="index">{{ problem }}</li>
          </ul>
        </div>
      </div>

      <div class="fields">
        <BaseField :label="t('invoiceEmail.from')" for="email-from">
          <input id="email-from" class="control" type="text" :value="compose.from ?? t('invoiceEmail.gmailNotConnected')" readonly />
        </BaseField>
        <BaseField :label="t('invoiceEmail.template')" for="email-template">
          <select id="email-template" v-model="templateId" class="control" :disabled="switching" @change="switchTemplate">
            <option v-for="option in compose.templates" :key="option.id ?? 'default'" :value="option.id">
              {{ option.name }}{{ option.isActive ? ` (${t('templates.activeSuffix')})` : '' }}
            </option>
          </select>
        </BaseField>
        <BaseField :label="t('invoiceEmail.to')" for="email-to" required :error="fieldErrors.to" class="span-2">
          <input id="email-to" v-model="form.to" class="control" type="text" autocomplete="off" placeholder="accounts@example.com" />
        </BaseField>
        <BaseField :label="t('customers.form.ccLabel')" for="email-cc" optional :error="fieldErrors.cc" class="span-2" :help="t('customers.form.ccHelp')">
          <input id="email-cc" v-model="form.cc" class="control" type="text" autocomplete="off" />
        </BaseField>
        <BaseField :label="t('invoiceEmail.subject')" for="email-subject" required :error="fieldErrors.subject" class="span-2">
          <input id="email-subject" v-model="form.subject" class="control" type="text" maxlength="500" />
        </BaseField>
      </div>

      <div class="body-header">
        <span class="label">{{ t('invoiceEmail.message') }}</span>
        <div class="switch" role="group" :aria-label="t('invoiceEmail.messageView')">
          <button type="button" :class="{ active: bodyView === 'preview' }" :aria-pressed="bodyView === 'preview'" @click="bodyView = 'preview'">{{ t('invoiceEmail.preview') }}</button>
          <button type="button" :class="{ active: bodyView === 'html' }" :aria-pressed="bodyView === 'html'" @click="bodyView = 'html'">{{ t('invoiceEmail.editHtml') }}</button>
        </div>
      </div>
      <iframe
        v-if="bodyView === 'preview'"
        class="body-preview"
        :title="t('invoiceEmail.previewTitle')"
        sandbox=""
        :srcdoc="previewDocument"
      ></iframe>
      <textarea v-else v-model="form.body" class="control body-html" spellcheck="false" :aria-label="t('invoiceEmail.messageHtml')"></textarea>
      <p v-if="fieldErrors.body" class="field-error" role="alert">{{ fieldErrors.body }}</p>

      <fieldset class="attachments">
        <legend>{{ t('invoiceEmail.attachments') }}</legend>
        <label v-for="attachment in compose.attachments" :key="attachment.key" class="attachment">
          <input
            type="checkbox"
            :checked="attachment.key === 'invoice' || includeMonthlyReport"
            :disabled="attachment.key === 'invoice'"
            @change="attachment.key === 'monthlyReport' && (includeMonthlyReport = ($event.target as HTMLInputElement).checked)"
          />
          <AppIcon name="invoices" />
          {{ attachment.fileName }}
        </label>
      </fieldset>
    </form>

    <template #footer>
      <template v-if="created">
        <BaseButton @click="emit('close')">{{ t('common.close') }}</BaseButton>
        <BaseButton v-if="canMarkSent" icon="check" @click="emit('mark-sent')">{{ t('invoices.actions.markSent') }}</BaseButton>
        <a class="gmail-link" :href="created.gmailUrl" target="_blank" rel="noopener">
          <AppIcon name="send" />{{ t('invoices.details.openInGmail') }}
        </a>
      </template>
      <template v-else>
        <BaseButton :disabled="saving" @click="emit('close')">{{ t('common.cancel') }}</BaseButton>
        <BaseButton
          type="submit"
          form="invoice-email-form"
          variant="primary"
          icon="send"
          :loading="saving"
          :disabled="!compose || !gmailReady || switching"
        >
          {{ t('invoiceEmail.create') }}
        </BaseButton>
      </template>
    </template>
  </BaseDialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { invoicesApi } from '@/api/invoices'
import type { InvoiceDto, InvoiceEmailComposeDto, InvoiceEmailDto } from '@/types'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { useToast, apiErrorCode, errorMessage } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import { invoiceStatus } from '@/utils/format'
import { currentLanguage } from '@/i18n'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps<{
  open: boolean
  invoice: InvoiceDto
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'created', email: InvoiceEmailDto): void
  (e: 'mark-sent'): void
}>()

const toast = useToast()
const { confirm } = useConfirm()

const compose = ref<InvoiceEmailComposeDto | null>(null)
const loading = ref(false)
const loadError = ref<string | null>(null)
const switching = ref(false)
const saving = ref(false)
const created = ref<InvoiceEmailDto | null>(null)
const templateId = ref<number | null>(null)
const includeMonthlyReport = ref(true)
const bodyView = ref<'preview' | 'html'>('preview')
/** Set when creating the draft revealed that Gmail must be (re)connected. */
const gmailProblem = ref<string | null>(null)

const form = reactive({ to: '', cc: '', subject: '', body: '' })
const fieldErrors = reactive<Record<string, string | undefined>>({})
let loadedSnapshot = ''

const EMAIL_PATTERN = /^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$/

const canMarkSent = computed(() => invoiceStatus(props.invoice.status).name === 'Finalized')
const gmailReady = computed(() => !!compose.value?.from && !gmailProblem.value)
const notConnectedMessage = computed(() =>
  gmailProblem.value ?? t('invoiceEmail.connectGmail'))

/** ['A', 'B'] → "A and B" in the app's language */
function listOf(items: string[]): string {
  return new Intl.ListFormat(currentLanguage(), { type: 'conjunction' }).format(items)
}

// The rendered HTML inside a minimal page so it reads like an email client would show it
const previewDocument = computed(() => `<!DOCTYPE html><html><head><meta charset="utf-8"><style>
  body { margin: 16px; font: 14px/1.5 Arial, Helvetica, sans-serif; color: #202124; background: #fff; }
</style></head><body>${form.body}</body></html>`)

function snapshot() {
  return JSON.stringify([form.to, form.cc, form.subject, form.body])
}

function apply(result: InvoiceEmailComposeDto, keepRecipients: boolean) {
  compose.value = result
  templateId.value = result.templateId
  if (!keepRecipients) {
    form.to = result.to
    form.cc = result.cc ?? ''
  }
  form.subject = result.subject
  form.body = result.body
  loadedSnapshot = snapshot()
}

async function load(keepRecipients = false) {
  loading.value = !keepRecipients
  loadError.value = null
  try {
    apply(await invoicesApi.composeEmail(props.invoice.id, templateId.value), keepRecipients)
  } catch (error) {
    loadError.value = errorMessage(error)
  } finally {
    loading.value = false
  }
}

async function switchTemplate() {
  const previous = compose.value?.templateId ?? null
  const edited = snapshot() !== loadedSnapshot
  if (edited && !(await confirm({
    title: t('invoiceEmail.replaceTitle'),
    message: t('invoiceEmail.replaceMessage'),
    confirmLabel: t('invoiceEmail.switchTemplate')
  }))) {
    templateId.value = previous
    return
  }
  switching.value = true
  try {
    await load(true)
  } finally {
    switching.value = false
  }
}

function validate(): boolean {
  Object.keys(fieldErrors).forEach(key => delete fieldErrors[key])
  const to = form.to.split(/[,;\s]+/).filter(Boolean)
  const cc = form.cc.split(/[,;\s]+/).filter(Boolean)
  if (to.length === 0) fieldErrors.to = t('invoiceEmail.addRecipient')
  else if (to.some(a => !EMAIL_PATTERN.test(a))) fieldErrors.to = t('customers.form.invalidAddress', { address: to.find(a => !EMAIL_PATTERN.test(a)) ?? '' })
  if (cc.some(a => !EMAIL_PATTERN.test(a))) fieldErrors.cc = t('customers.form.invalidAddress', { address: cc.find(a => !EMAIL_PATTERN.test(a)) ?? '' })
  if (!form.subject.trim()) fieldErrors.subject = t('invoiceEmail.enterSubject')
  if (!form.body.trim()) fieldErrors.body = t('invoiceEmail.emptyMessage')
  return Object.keys(fieldErrors).length === 0
}

async function createDraft() {
  if (!compose.value || !validate()) return
  saving.value = true
  try {
    const email = await invoicesApi.createEmailDraft(props.invoice.id, {
      to: form.to,
      cc: form.cc || null,
      subject: form.subject,
      body: form.body,
      includeMonthlyReport: includeMonthlyReport.value
    })
    created.value = email
    emit('created', email)
  } catch (error) {
    const code = apiErrorCode(error)
    if (code === 'gmail_not_connected' || code === 'gmail_reconnect_required' || code === 'gmail_not_configured') {
      gmailProblem.value = errorMessage(error)
    } else {
      toast.failure(t('invoiceEmail.createFailed'), error)
    }
  } finally {
    saving.value = false
  }
}

// Every opening starts fresh from the template
watch(
  () => props.open,
  isOpen => {
    if (!isOpen) return
    compose.value = null
    created.value = null
    gmailProblem.value = null
    templateId.value = null
    includeMonthlyReport.value = true
    bodyView.value = 'preview'
    Object.keys(fieldErrors).forEach(key => delete fieldErrors[key])
    load()
  },
  { immediate: true }
)
</script>

<style scoped>
.compose {
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
}

.fields {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.8rem 1rem;
}

.span-2 {
  grid-column: span 2;
}

@media (max-width: 640px) {
  .fields {
    grid-template-columns: minmax(0, 1fr);
  }

  .span-2 {
    grid-column: auto;
  }
}

.notice {
  display: flex;
  gap: 0.6rem;
  align-items: flex-start;
  padding: 0.65rem 0.8rem;
  border: 1px solid;
  border-radius: var(--radius-md);
  font-size: var(--text-sm);
}

.notice .app-icon {
  flex: none;
  width: 1.1rem;
  height: 1.1rem;
  margin-top: 0.1rem;
}

.notice ul {
  margin: 0.3rem 0 0;
  padding-left: 1.1rem;
}

.notice.warning {
  color: var(--color-warning);
  background: var(--color-warning-soft);
  border-color: var(--color-warning-line);
}

.notice.danger {
  color: var(--color-danger);
  background: var(--color-danger-soft);
  border-color: var(--color-danger-line);
}

.body-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.label,
.attachments legend {
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--color-text-secondary);
}

.switch {
  display: inline-flex;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  overflow: hidden;
}

.switch button {
  padding: 0.25rem 0.7rem;
  border: 0;
  background: var(--color-surface);
  font-size: var(--text-sm);
  color: var(--color-text-muted);
  cursor: pointer;
}

.switch button.active {
  background: var(--color-primary-soft);
  color: var(--color-primary);
  font-weight: 600;
}

.body-preview,
.body-html {
  width: 100%;
  height: 18rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
}

/* The preview is always shown on white, as most mail clients do */
.body-preview {
  background: #fff;
}

.body-html {
  resize: vertical;
  font-family: var(--font-mono, ui-monospace, monospace);
  font-size: var(--text-sm);
}

.field-error {
  margin: -0.4rem 0 0;
  font-size: var(--text-sm);
  color: var(--color-danger);
}

.attachments {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem 1.25rem;
  margin: 0;
  padding: 0;
  border: 0;
}

.attachments legend {
  margin-bottom: 0.4rem;
}

.attachment {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-size: var(--text-sm);
}

.attachment .app-icon {
  width: 1rem;
  height: 1rem;
  color: var(--color-text-muted);
}

.done p {
  margin: 0 0 0.6rem;
}

.muted {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.gmail-link {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.5rem 0.95rem;
  border-radius: var(--radius-md);
  background: var(--color-primary);
  color: var(--color-on-primary, #fff);
  font-weight: 600;
  text-decoration: none;
}

.gmail-link .app-icon {
  width: 1rem;
  height: 1rem;
}
</style>
