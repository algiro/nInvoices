<template>
  <div class="template-editor">
    <TemplateEditorHeader
      v-model:name="name"
      v-model:invoice-type="invoiceType"
      v-model:layout="layout"
      v-model:show-variables="showVariables"
      :kind="kind"
      :customer-id="customerId"
      :customer-name="customerName"
      :is-new="isNew"
      :is-active="isActive"
      :saving="saving"
      :dirty="dirty"
      :loading="loadingTemplate"
      @load-sample="loadSample"
      @save="save"
    />

    <div v-if="kind === 'email' && !loadingTemplate" class="subject-row">
      <label for="template-subject">Subject</label>
      <div class="subject-field">
        <input
          id="template-subject"
          v-model="subject"
          class="subject-input"
          type="text"
          maxlength="500"
          placeholder="Invoice [[ invoiceNumber ]] - [[ customer.name ]]"
          spellcheck="false"
        />
        <span class="subject-preview" :title="preview.subject.value ?? ''">
          <template v-if="preview.subject.value">Preview: {{ preview.subject.value }}</template>
        </span>
      </div>
    </div>

    <div v-if="loadingTemplate" class="loading">Loading template…</div>

    <div v-else class="workspace" :class="[`layout-${layout}`, { 'with-variables': showVariables }]">
      <TemplateVariablesPanel v-if="showVariables" class="pane variables-pane" :groups="variableGroups" @insert="insertSnippet" />

      <section v-show="layout !== 'preview'" class="pane code-pane" aria-label="Template code">
        <TemplateCodeEditor
          ref="codeEditor"
          v-model="content"
          :variables="variableGroups"
          :problems="preview.problems.value"
          class="code"
          @save="save"
        />
        <TemplateProblems
          v-if="preview.errors.value.length"
          :errors="preview.errors.value"
          :has-syntax-errors="preview.hasSyntaxErrors.value"
          @go-to="(line, column) => codeEditor?.goToLine(line, column)"
        />
      </section>

      <TemplatePreviewPane
        v-show="layout !== 'code'"
        class="pane preview-pane"
        :html="preview.html.value"
        :loading="preview.busy.value"
        :stale="preview.stale.value"
        :customer-name="customerName"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter, onBeforeRouteLeave } from 'vue-router'
import TemplateCodeEditor from '@/components/templates/editor/TemplateCodeEditor.vue'
import TemplatePreviewPane from '@/components/templates/editor/TemplatePreviewPane.vue'
import TemplateVariablesPanel from '@/components/templates/editor/TemplateVariablesPanel.vue'
import TemplateEditorHeader from '@/components/templates/editor/TemplateEditorHeader.vue'
import TemplateProblems from '@/components/templates/editor/TemplateProblems.vue'
import { variablesFor, type TemplateKind } from '@/components/templates/editor/templateVariables'
import { templateKinds, type InvoiceTypeName, type TemplateDraft } from '@/components/templates/editor/templateKinds'
import { useTemplatePreview } from '@/components/templates/editor/useTemplatePreview'
import { useEditorLayout } from '@/components/templates/editor/useEditorLayout'
import {
  invoiceSample, monthlyReportSample, monthlyReportSampleName,
  emailSampleName, emailSampleSubject, emailSampleBody
} from '@/components/templates/editor/templateSamples'
import { useCustomersStore } from '@/stores/customers'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'

const route = useRoute()
const router = useRouter()
const customersStore = useCustomersStore()
const toast = useToast()
const { confirm } = useConfirm()

const kind = computed(() => route.meta.kind as TemplateKind)
const api = computed(() => templateKinds[kind.value])
// No customer in the URL: a template shared by all customers
const customerId = computed(() => (route.params.id ? Number(route.params.id) : null))
const isShared = computed(() => customerId.value === null)
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
const subject = ref('') // email templates only
const invoiceType = ref<InvoiceTypeName>('Monthly')
const isActive = ref(false)
const loadingTemplate = ref(false)
const saving = ref(false)
const codeEditor = ref<InstanceType<typeof TemplateCodeEditor> | null>(null)

const { layout, showVariables } = useEditorLayout()

const preview = useTemplatePreview({
  api: () => api.value,
  content,
  subject,
  customerId: () => customerId.value,
  loading: loadingTemplate
})

// ---------- dirty tracking ----------

const saved = ref('')
const snapshot = () => JSON.stringify([name.value, content.value, invoiceType.value, subject.value])
const dirty = computed(() => !loadingTemplate.value && snapshot() !== saved.value)

// ---------- loading ----------

async function load() {
  loadingTemplate.value = true
  try {
    if (customerId.value !== null) {
      const customer = customersStore.selectedCustomer?.id === customerId.value
        ? customersStore.selectedCustomer
        : await customersStore.fetchById(customerId.value)
      customerName.value = customer?.name ?? ''
    }

    if (templateId.value !== null) {
      const t = await api.value.load(templateId.value)
      name.value = t.name
      content.value = t.content
      isActive.value = t.isActive
      if (t.subject !== undefined) subject.value = t.subject
      if (t.invoiceType !== undefined) invoiceType.value = t.invoiceType
    } else {
      // A new template starts from the sample, so the preview shows something right away
      applySample()
    }
    saved.value = isNew.value ? JSON.stringify(['', '', invoiceType.value, '']) : snapshot()
  } catch (error) {
    toast.failure('Could not load the template', error)
  } finally {
    loadingTemplate.value = false
  }
}

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
  applySample(true)
}

/** Fills the editor with this kind's sample; `keepName` leaves a name already typed. */
function applySample(keepName = false) {
  if (kind.value === 'email') {
    subject.value = emailSampleSubject
    content.value = emailSampleBody
    if (!keepName || !name.value.trim()) name.value = emailSampleName
  } else if (kind.value === 'invoice') {
    content.value = invoiceSample
    if (!keepName) name.value = ''
  } else {
    content.value = monthlyReportSample
    if (!keepName || !name.value.trim()) name.value = monthlyReportSampleName
  }
}

async function save() {
  if (saving.value || loadingTemplate.value) return
  if (kind.value !== 'invoice' && !name.value.trim()) {
    toast.warning('Give the template a name', {
      message: kind.value === 'email'
        ? 'Email templates need a name so you can pick them when creating an email.'
        : 'Monthly report templates need a name so you can pick them when generating an invoice.'
    })
    document.getElementById('template-name')?.focus()
    return
  }
  if (kind.value === 'email' && !subject.value.trim()) {
    toast.warning('The subject is empty')
    document.getElementById('template-subject')?.focus()
    return
  }
  if (!content.value.trim()) {
    toast.warning('The template is empty')
    return
  }
  // Make sure the check reflects the latest text before deciding
  await preview.flush()
  if (preview.hasSyntaxErrors.value) {
    toast.error('Fix the template errors before saving', { message: 'They are listed under the editor; click one to jump to it.' })
    return
  }

  saving.value = true
  try {
    const draft: TemplateDraft = { name: name.value.trim(), content: content.value, subject: subject.value, invoiceType: invoiceType.value }
    let createdId: number | null = null
    if (isNew.value) {
      const created = await api.value.create(draft, customerId.value)
      createdId = created.id
      if (created.isActive !== undefined) isActive.value = created.isActive
    } else {
      await api.value.update(templateId.value!, draft, isActive.value)
    }

    saved.value = snapshot()
    toast.success('Template saved')

    if (createdId !== null) {
      await router.replace(isShared.value
        ? `/templates/${kind.value}/${createdId}`
        : `/customers/${customerId.value}/templates/${kind.value}/${createdId}`)
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
  preview.refresh()
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown)
  window.removeEventListener('beforeunload', onBeforeUnload)
})

// Switching between templates in place (e.g. after "Save" on a new one) reloads nothing:
// the content is already current. A different template id loads that template.
watch(templateId, (next, previous) => {
  if (next !== null && previous !== null && next !== previous) load()
})
</script>

<style scoped>
.subject-row {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
}

.subject-row label {
  padding-top: 0.4rem;
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--color-text-secondary);
}

.subject-field {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.subject-input {
  width: 100%;
  padding: 0.4rem 0.55rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  font-family: var(--font-mono, ui-monospace, monospace);
  font-size: var(--text-sm);
  color: var(--color-text);
}

.subject-input:focus {
  outline: none;
  border-color: var(--color-primary);
  box-shadow: var(--focus-ring);
}

.subject-preview {
  min-height: 1.1rem;
  font-size: var(--text-xs);
  color: var(--color-text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.template-editor {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  /* fill the viewport under the top bar (content area has 1.5rem padding) */
  height: calc(100vh - var(--topbar-height) - 3rem);
  min-height: 32rem;
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
