<template>
  <div class="template-editor">
    <header class="editor-header">
      <router-link :to="backLink" class="back">
        <AppIcon name="chevronRight" class="back-icon" />
        {{ customerName || 'Customer' }} · {{ kind === 'invoice' ? 'Invoice templates' : 'Monthly reports' }}
      </router-link>

      <div class="header-row">
        <input
          id="template-name"
          v-model="name"
          class="name-input"
          type="text"
          maxlength="200"
          :placeholder="kind === 'invoice' ? 'Untitled invoice template' : 'Template name (required)'"
          aria-label="Template name"
        />

        <label v-if="kind === 'invoice'" class="type-field">
          <span>Invoice type</span>
          <select
            id="template-invoice-type"
            v-model="invoiceType"
            :disabled="!isNew"
            :title="isNew ? '' : 'The invoice type is fixed once a template is created'"
          >
            <option value="Monthly">Monthly</option>
            <option value="OneTime">One-time</option>
          </select>
        </label>

        <span v-if="!isNew" class="pill" :class="isActive ? 'active' : 'inactive'">
          {{ isActive ? 'Active' : 'Inactive' }}
        </span>

        <span class="save-state" :class="{ dirty }">
          {{ saving ? 'Saving…' : dirty ? 'Unsaved changes' : isNew ? 'Not saved yet' : 'All changes saved' }}
        </span>

        <span class="grow"></span>

        <div class="layout-switch" role="group" aria-label="Layout">
          <button type="button" :class="{ active: showVariables }" :aria-pressed="showVariables" @click="showVariables = !showVariables">
            Variables
          </button>
          <button
            v-for="mode in layoutModes"
            :key="mode.value"
            type="button"
            :class="{ active: layout === mode.value }"
            :aria-pressed="layout === mode.value"
            @click="layout = mode.value"
          >
            {{ mode.label }}
          </button>
        </div>

        <BaseButton variant="ghost" @click="loadSample">Load sample</BaseButton>
        <BaseButton variant="primary" :loading="saving" :disabled="loadingTemplate" @click="save">
          Save <kbd class="kbd">Ctrl S</kbd>
        </BaseButton>
      </div>
    </header>

    <div v-if="loadingTemplate" class="loading">Loading template…</div>

    <div v-else class="workspace" :class="[`layout-${layout}`, { 'with-variables': showVariables }]">
      <TemplateVariablesPanel v-if="showVariables" class="pane variables-pane" :groups="variableGroups" @insert="insertSnippet" />

      <section v-show="layout !== 'preview'" class="pane code-pane" aria-label="Template code">
        <TemplateCodeEditor
          ref="codeEditor"
          v-model="content"
          :variables="variableGroups"
          :problems="problems"
          class="code"
          @save="save"
        />
        <div v-if="errors.length" class="problems" role="alert">
          <div class="problems-title">
            <AppIcon name="error" />
            {{ errors.length === 1 ? '1 problem' : `${errors.length} problems` }}: {{ hasSyntaxErrors ? 'fix before saving' : 'the preview can’t render with sample data' }}
          </div>
          <button
            v-for="(error, index) in errors"
            :key="index"
            type="button"
            class="problem"
            :disabled="!error.line"
            @click="error.line && codeEditor?.goToLine(error.line, error.column)"
          >
            <span v-if="error.line" class="where">Line {{ error.line }}</span>
            <span class="what">{{ error.message }}</span>
          </button>
        </div>
      </section>

      <TemplatePreviewPane
        v-show="layout !== 'code'"
        class="pane preview-pane"
        :html="previewHtml"
        :loading="previewLoading"
        :stale="previewStale"
        :customer-name="customerName"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter, onBeforeRouteLeave } from 'vue-router'
import AppIcon from '@/components/ui/AppIcon.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import TemplateCodeEditor, { type EditorProblem } from '@/components/templates/editor/TemplateCodeEditor.vue'
import TemplatePreviewPane from '@/components/templates/editor/TemplatePreviewPane.vue'
import TemplateVariablesPanel from '@/components/templates/editor/TemplateVariablesPanel.vue'
import { variablesFor, type TemplateKind } from '@/components/templates/editor/templateVariables'
import { invoiceSample, monthlyReportSample, monthlyReportSampleName } from '@/components/templates/editor/templateSamples'
import { templatesApi } from '@/api/templates'
import { monthlyReportTemplatesApi } from '@/api/monthlyReportTemplates'
import { useCustomersStore } from '@/stores/customers'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import type { InvoiceType } from '@/types'

type Layout = 'split' | 'code' | 'preview'
type InvoiceTypeName = 'Monthly' | 'OneTime'

const route = useRoute()
const router = useRouter()
const customersStore = useCustomersStore()
const toast = useToast()
const { confirm } = useConfirm()

const kind = computed(() => route.meta.kind as TemplateKind)
const customerId = computed(() => Number(route.params.id))
// 'new' in the URL means an unsaved template
const templateId = computed(() => {
  const raw = route.params.templateId
  return raw && raw !== 'new' ? Number(raw) : null
})
const isNew = computed(() => templateId.value === null)
const variableGroups = computed(() => variablesFor(kind.value))

const customerName = ref('')
const name = ref('')
const content = ref('')
const invoiceType = ref<InvoiceTypeName>('Monthly')
const isActive = ref(false)
const loadingTemplate = ref(false)
const saving = ref(false)
const codeEditor = ref<InstanceType<typeof TemplateCodeEditor> | null>(null)

const backLink = computed(() => ({
  path: `/customers/${customerId.value}`,
  query: { tab: kind.value === 'invoice' ? 'templates' : 'monthly-reports' }
}))

// ---------- layout preferences (per browser) ----------

const LAYOUT_KEY = 'ninvoices.templateEditor.layout'
const VARIABLES_KEY = 'ninvoices.templateEditor.variables'
const layoutModes: { value: Layout; label: string }[] = [
  { value: 'code', label: 'Code' },
  { value: 'split', label: 'Split' },
  { value: 'preview', label: 'Preview' }
]
const layout = ref<Layout>(readPref(LAYOUT_KEY, 'split') as Layout)
const showVariables = ref(readPref(VARIABLES_KEY, window.innerWidth >= 1200 ? 'true' : 'false') === 'true')

function readPref(key: string, fallback: string): string {
  try {
    return localStorage.getItem(key) ?? fallback
  } catch {
    return fallback
  }
}

watch(layout, value => { try { localStorage.setItem(LAYOUT_KEY, value) } catch { /* not remembered */ } })
watch(showVariables, value => { try { localStorage.setItem(VARIABLES_KEY, String(value)) } catch { /* not remembered */ } })

// ---------- dirty tracking ----------

const saved = ref('')
const snapshot = () => JSON.stringify([name.value, content.value, invoiceType.value])
const dirty = computed(() => !loadingTemplate.value && snapshot() !== saved.value)

// ---------- loading ----------

async function load() {
  loadingTemplate.value = true
  try {
    const customer = customersStore.selectedCustomer?.id === customerId.value
      ? customersStore.selectedCustomer
      : await customersStore.fetchById(customerId.value)
    customerName.value = customer?.name ?? ''

    if (templateId.value !== null) {
      if (kind.value === 'invoice') {
        const t = await templatesApi.getById(templateId.value)
        name.value = t.name
        content.value = t.content
        invoiceType.value = (String(t.invoiceType) as InvoiceTypeName) ?? 'Monthly'
        isActive.value = t.isActive
      } else {
        const t = await monthlyReportTemplatesApi.getById(templateId.value)
        name.value = t.name
        content.value = t.content
        isActive.value = t.isActive
      }
    } else {
      // A new template starts from the sample, so the preview shows something right away
      name.value = kind.value === 'invoice' ? '' : monthlyReportSampleName
      content.value = kind.value === 'invoice' ? invoiceSample : monthlyReportSample
    }
    saved.value = isNew.value ? JSON.stringify(['', '', invoiceType.value]) : snapshot()
  } catch (error) {
    toast.failure('Could not load the template', error)
  } finally {
    loadingTemplate.value = false
  }
}

// ---------- live preview ----------

interface ParsedError extends EditorProblem {
  syntax: boolean
}

const previewHtml = ref<string | null>(null)
const previewLoading = ref(false)
const previewStale = ref(false)
const errors = ref<ParsedError[]>([])
const problems = computed<EditorProblem[]>(() => errors.value.filter(e => e.line > 0))
const hasSyntaxErrors = computed(() => errors.value.some(e => e.syntax))

let previewTimer: ReturnType<typeof setTimeout> | null = null
let previewSeq = 0

function parseError(raw: string): ParsedError {
  // Parser: "Line 3, Column 12: message"
  let m = raw.match(/^Line (\d+), Column (\d+):\s*([\s\S]*)$/)
  if (m) return { line: +m[1], column: +m[2], message: m[3], syntax: true }
  // Runtime: "<input>(3,12) : error : message"
  m = raw.match(/^<input>\((\d+),(\d+)\)\s*:\s*error\s*:\s*([\s\S]*)$/)
  if (m) return { line: +m[1], column: +m[2], message: m[3], syntax: false }
  return { line: 0, column: 0, message: raw, syntax: false }
}

async function refreshPreview() {
  const seq = ++previewSeq
  if (!content.value.trim()) {
    previewHtml.value = null
    errors.value = []
    previewStale.value = false
    return
  }
  previewLoading.value = true
  try {
    const api = kind.value === 'invoice' ? templatesApi : monthlyReportTemplatesApi
    const result = await api.preview(content.value, customerId.value)
    if (seq !== previewSeq) return // a newer edit is already on its way
    errors.value = result.errors.map(parseError)
    if (result.html !== null) {
      previewHtml.value = result.html
      previewStale.value = false
    } else {
      previewStale.value = previewHtml.value !== null
    }
  } catch (error) {
    if (seq === previewSeq) toast.failure('Preview failed', error)
  } finally {
    if (seq === previewSeq) previewLoading.value = false
  }
}

watch(content, () => {
  if (loadingTemplate.value) return
  if (previewTimer) clearTimeout(previewTimer)
  previewTimer = setTimeout(refreshPreview, 500)
})

// ---------- actions ----------

function insertSnippet(text: string) {
  if (layout.value === 'preview') layout.value = 'split'
  // the editor may have just been shown; give it a frame to lay out
  requestAnimationFrame(() => codeEditor.value?.insert(text))
}

async function loadSample() {
  if (content.value.trim() && !(await confirm({
    title: 'Replace the template?',
    message: 'Loading the sample replaces everything currently in the editor.',
    confirmLabel: 'Replace'
  }))) return
  content.value = kind.value === 'invoice' ? invoiceSample : monthlyReportSample
  if (kind.value === 'monthly-report' && !name.value.trim()) name.value = monthlyReportSampleName
}

async function save() {
  if (saving.value || loadingTemplate.value) return
  if (kind.value === 'monthly-report' && !name.value.trim()) {
    toast.warning('Give the template a name', { message: 'Monthly report templates need a name so you can pick them when generating an invoice.' })
    document.getElementById('template-name')?.focus()
    return
  }
  if (!content.value.trim()) {
    toast.warning('The template is empty')
    return
  }
  // Make sure the check reflects the latest text before deciding
  if (previewTimer) {
    clearTimeout(previewTimer)
    previewTimer = null
    await refreshPreview()
  }
  if (hasSyntaxErrors.value) {
    toast.error('Fix the template errors before saving', { message: 'They are listed under the editor; click one to jump to it.' })
    return
  }

  saving.value = true
  try {
    let createdId: number | null = null
    if (kind.value === 'invoice') {
      if (isNew.value) {
        const created = await templatesApi.create({
          customerId: customerId.value,
          invoiceType: invoiceType.value as unknown as InvoiceType,
          name: name.value.trim(),
          content: content.value
        })
        createdId = created.id
      } else {
        await templatesApi.update(templateId.value!, {
          invoiceType: invoiceType.value as unknown as InvoiceType,
          name: name.value.trim(),
          content: content.value,
          isActive: isActive.value // keep the current state; the API defaults to active
        })
      }
    } else if (isNew.value) {
      const created = await monthlyReportTemplatesApi.create({
        customerId: customerId.value,
        name: name.value.trim(),
        content: content.value
      })
      createdId = created.id
    } else {
      await monthlyReportTemplatesApi.update(templateId.value!, { name: name.value.trim(), content: content.value })
    }

    saved.value = snapshot()
    toast.success('Template saved')

    if (createdId !== null) {
      const segment = kind.value === 'invoice' ? 'invoice' : 'monthly-report'
      await router.replace(`/customers/${customerId.value}/templates/${segment}/${createdId}`)
    }
  } catch (error) {
    toast.failure('Could not save the template', error)
  } finally {
    saving.value = false
  }
}

// ---------- keyboard & leaving ----------

// Ctrl+S anywhere on the page saves. Inside the code editor CodeMirror's own binding handles it.
function onKeydown(event: KeyboardEvent) {
  if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 's') return
  if ((event.target as HTMLElement | null)?.closest('.cm-editor')) return
  event.preventDefault() // never the browser's "Save page"
  if (!event.repeat) save()
}

function onBeforeUnload(event: BeforeUnloadEvent) {
  if (dirty.value) event.preventDefault()
}

onBeforeRouteLeave(async () => {
  if (!dirty.value) return true
  return confirm({
    title: 'Leave without saving?',
    message: 'Your changes to this template will be lost.',
    confirmLabel: 'Discard changes',
    cancelLabel: 'Keep editing',
    tone: 'danger'
  })
})

onMounted(async () => {
  window.addEventListener('keydown', onKeydown)
  window.addEventListener('beforeunload', onBeforeUnload)
  await load()
  refreshPreview()
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown)
  window.removeEventListener('beforeunload', onBeforeUnload)
  if (previewTimer) clearTimeout(previewTimer)
})

// Switching between templates in place (e.g. after "Save" on a new one) reloads nothing:
// the content is already current. A different template id loads that template.
watch(templateId, (next, previous) => {
  if (next !== null && previous !== null && next !== previous) load()
})
</script>

<style scoped>
.template-editor {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  /* fill the viewport under the top bar (content area has 1.5rem padding) */
  height: calc(100vh - var(--topbar-height) - 3rem);
  min-height: 32rem;
}

.editor-header {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.back {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  width: fit-content;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.back-icon {
  width: 0.9rem;
  height: 0.9rem;
  transform: rotate(180deg);
}

.header-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.6rem 0.75rem;
}

.name-input {
  flex: 0 1 24rem;
  min-width: 12rem;
  padding: 0.3rem 0.5rem;
  margin-left: -0.5rem;
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  background: transparent;
  font-size: var(--text-lg);
  font-weight: 600;
  color: var(--color-text);
}

.name-input:hover {
  border-color: var(--color-border);
}

.name-input:focus {
  outline: none;
  border-color: var(--color-primary);
  background: var(--color-surface);
  box-shadow: var(--focus-ring);
}

.type-field {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.type-field select {
  padding: 0.3rem 0.45rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  font-size: var(--text-sm);
}

.pill {
  font-size: var(--text-xs);
  font-weight: 600;
  padding: 0.1rem 0.55rem;
  border-radius: 999px;
  border: 1px solid;
}

.pill.active {
  color: var(--color-success);
  background: var(--color-success-soft);
  border-color: var(--color-success-line);
}

.pill.inactive {
  color: var(--color-text-muted);
  background: var(--color-surface-muted);
  border-color: var(--color-border-strong);
}

.save-state {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.save-state.dirty {
  color: var(--color-warning);
  font-weight: 500;
}

.grow {
  flex: 1;
}

.layout-switch {
  display: inline-flex;
  gap: 2px;
  padding: 2px;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
}

.layout-switch button {
  padding: 0.25rem 0.6rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
}

.layout-switch button:hover {
  background: var(--color-surface-sunken);
  color: var(--color-text);
}

.layout-switch button.active {
  background: var(--color-primary);
  color: var(--color-on-primary);
}

.kbd {
  margin-left: 0.25rem;
  padding: 0 0.3rem;
  border-radius: var(--radius-sm);
  background: color-mix(in srgb, var(--color-on-primary) 16%, transparent);
  font-size: 0.68rem;
  font-family: var(--font-mono);
}

.loading {
  padding: 3rem;
  text-align: center;
  color: var(--color-text-muted);
}

/* ---------- workspace ---------- */
.workspace {
  flex: 1;
  min-height: 0;
  display: grid;
  gap: 0;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  overflow: hidden;
  background: var(--color-border);
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
}

.workspace.with-variables { grid-template-columns: 15rem minmax(0, 1fr) minmax(0, 1fr); }
.workspace.layout-code,
.workspace.layout-preview { grid-template-columns: minmax(0, 1fr); }
.workspace.layout-code.with-variables,
.workspace.layout-preview.with-variables { grid-template-columns: 15rem minmax(0, 1fr); }

.pane {
  min-height: 0;
  min-width: 0;
}

.variables-pane {
  border-right: 1px solid var(--color-border);
}

.code-pane {
  display: flex;
  flex-direction: column;
  background: var(--color-surface);
}

.preview-pane {
  border-left: 1px solid var(--color-border);
}

.layout-preview .preview-pane {
  border-left: 0;
}

.code {
  flex: 1;
  min-height: 0;
}

.problems {
  max-height: 9rem;
  overflow-y: auto;
  border-top: 1px solid var(--color-danger-line);
  background: var(--color-danger-soft);
  font-size: var(--text-sm);
}

.problems-title {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.4rem 0.75rem 0.2rem;
  font-weight: 600;
  color: var(--color-danger);
}

.problem {
  width: 100%;
  display: flex;
  gap: 0.6rem;
  padding: 0.25rem 0.75rem 0.25rem 2.1rem;
  border: 0;
  border-radius: 0;
  background: transparent;
  text-align: left;
  color: var(--color-text-secondary);
  cursor: pointer;
}

.problem:hover:not(:disabled) {
  background: color-mix(in srgb, var(--color-danger) 8%, transparent);
  color: var(--color-text);
}

.problem:disabled {
  cursor: default;
  opacity: 1;
}

.where {
  flex: none;
  font-family: var(--font-mono);
  font-size: var(--text-xs);
  color: var(--color-danger);
  padding-top: 0.1rem;
}

.what {
  overflow-wrap: anywhere;
}

@media (max-width: 1000px) {
  .template-editor {
    height: auto;
  }

  .workspace,
  .workspace.with-variables,
  .workspace.layout-code.with-variables,
  .workspace.layout-preview.with-variables {
    grid-template-columns: minmax(0, 1fr);
  }

  .code-pane { height: 60vh; }
  .preview-pane { height: 70vh; border-left: 0; border-top: 1px solid var(--color-border); }
  .variables-pane { max-height: 40vh; border-right: 0; border-bottom: 1px solid var(--color-border); }
}
</style>
