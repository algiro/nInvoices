<template>
  <section class="template-list">
    <header class="list-header">
      <div>
        <h3>{{ heading }}</h3>
        <p class="help">
          <template v-if="isShared">
            Shared by all your customers: one is used for every customer that has no active template of its own.
          </template>
          <template v-else-if="kind === 'invoice'">
            The active template for each invoice type is used when an invoice is generated. Without one, the shared template is used.
          </template>
          <template v-else-if="kind === 'email'">
            Subject and text of the email created in Gmail from an invoice. The active one is preselected; without one, the shared template or a built-in text is used.
          </template>
          <template v-else>
            The active template is used for the monthly timesheet PDF unless you pick another when generating. Without one, the shared template is used.
          </template>
        </p>
      </div>
      <BaseButton variant="primary" icon="plus" :to="editorLink('new')">New template</BaseButton>
    </header>

    <ul v-if="!isShared && !loading && !loadError && applies.length" class="applies" aria-label="Template in use">
      <li v-for="item in applies" :key="item.label" :class="item.tone">
        <AppIcon :name="item.tone === 'warn' ? 'alert' : 'template'" />
        <span><strong v-if="item.label">{{ item.label }}:</strong> {{ item.text }}</span>
      </li>
    </ul>

    <LoadingState v-if="loading" label="Loading templates…" />

    <EmptyState v-else-if="loadError" icon="alert" title="Templates could not be loaded" :description="loadError" compact>
      <BaseButton @click="load">Try again</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="rows.length === 0"
      icon="template"
      :title="!isShared && sharedRows.length ? 'No templates of its own' : 'No templates yet'"
      :description="!isShared && sharedRows.length
        ? 'This customer uses the shared templates below. Create its own only if it needs a different layout.'
        : 'Start from the sample and adjust it to your layout; the preview updates as you type.'"
      compact
    >
      <BaseButton variant="primary" icon="plus" :to="editorLink('new')">
        {{ !isShared && sharedRows.length ? 'Create its own template' : 'Create the first template' }}
      </BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">Name</th>
            <th v-if="kind === 'invoice'" scope="col">Invoice type</th>
            <th scope="col">Status</th>
            <th scope="col">Last changed</th>
            <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.id">
            <td>
              <router-link :to="editorLink(row.id)" class="primary-cell name">
                {{ row.name || `Untitled ${typeLabel(row.invoiceType).toLowerCase()} template` }}
              </router-link>
            </td>
            <td v-if="kind === 'invoice'">{{ typeLabel(row.invoiceType) }}</td>
            <td>
              <StatusPill :tone="row.isActive ? 'success' : 'neutral'">{{ row.isActive ? 'Active' : 'Inactive' }}</StatusPill>
              <span v-if="overridesShared(row)" class="override-note">overrides the shared one</span>
            </td>
            <td class="muted">{{ formatDate(row.updatedAt ?? row.createdAt) }}</td>
            <td class="actions">
              <BaseButton size="sm" variant="ghost" icon="edit" :to="editorLink(row.id)">Edit</BaseButton>
              <BaseButton
                v-if="row.isActive"
                size="sm"
                variant="ghost"
                :loading="busyId === row.id"
                @click="setActive(row, false)"
              >
                Deactivate
              </BaseButton>
              <BaseButton
                v-else
                size="sm"
                variant="ghost"
                icon="check"
                :loading="busyId === row.id"
                @click="setActive(row, true)"
              >
                Activate
              </BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                :aria-label="`Delete ${row.name || 'template'}`"
                title="Delete"
                @click="remove(row)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="!isShared && !loading && !loadError && sharedRows.length" class="shared-block">
      <h4>Shared templates</h4>
      <p class="help">
        Shared by all your customers; they apply here while this customer has no active template of its own.
        To change the layout for this customer only, override one.
      </p>
      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Name</th>
              <th v-if="kind === 'invoice'" scope="col">Invoice type</th>
              <th scope="col">Status</th>
              <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in sharedRows" :key="row.id">
              <td>
                <router-link :to="editorLink(row.id, true)" class="primary-cell name">
                  {{ row.name || `Untitled ${typeLabel(row.invoiceType).toLowerCase()} template` }}
                </router-link>
              </td>
              <td v-if="kind === 'invoice'">{{ typeLabel(row.invoiceType) }}</td>
              <td>
                <StatusPill :tone="row.isActive ? 'success' : 'neutral'">{{ row.isActive ? 'Active' : 'Inactive' }}</StatusPill>
              </td>
              <td class="actions">
                <BaseButton size="sm" variant="ghost" icon="edit" :to="editorLink(row.id, true)">Edit shared</BaseButton>
                <BaseButton
                  size="sm"
                  variant="secondary"
                  icon="plus"
                  :loading="busyId === row.id"
                  title="Copy it for this customer and use the copy"
                  @click="override(row)"
                >
                  Override for this customer
                </BaseButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import { templatesApi } from '@/api/templates'
import { monthlyReportTemplatesApi } from '@/api/monthlyReportTemplates'
import { emailTemplatesApi } from '@/api/emailTemplates'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import type { TemplateKind } from './editor/templateVariables'
import { formatDate } from '@/utils/format'

interface TemplateRow {
  id: number
  name: string
  invoiceType?: unknown
  isActive: boolean
  createdAt: string
  updatedAt?: string
  content?: string
  subject?: string
  body?: string
}

interface AppliesItem {
  label: string
  text: string
  tone: 'ok' | 'warn'
}

const props = defineProps<{
  kind: TemplateKind
  /** The customer whose templates these are; null for the templates shared by all customers. */
  customerId: number | null
}>()

const router = useRouter()
const toast = useToast()
const { confirm } = useConfirm()

const rows = ref<TemplateRow[]>([])
// The shared templates, listed under a customer's own (empty when this list is the shared one)
const sharedRows = ref<TemplateRow[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const busyId = ref<number | null>(null)

const isShared = computed(() => props.customerId === null)

const heading = computed(() => {
  const name = props.kind === 'invoice' ? 'Invoice templates' : props.kind === 'email' ? 'Email templates' : 'Monthly report templates'
  return isShared.value ? `Shared ${name.toLowerCase()}` : name
})

function editorLink(id: number | 'new', shared = false) {
  return shared || props.customerId === null
    ? `/templates/${props.kind}/${id}`
    : `/customers/${props.customerId}/templates/${props.kind}/${id}`
}

/** The operations every template kind shares. */
function apiFor(kind: TemplateKind) {
  return kind === 'invoice' ? templatesApi : kind === 'email' ? emailTemplatesApi : monthlyReportTemplatesApi
}

function typeLabel(type: unknown): string {
  const value = String(type)
  return value === 'OneTime' || value === '1' ? 'One-time' : value === 'Quarterly' ? 'Quarterly' : value === 'Annual' ? 'Annual' : 'Monthly'
}

const sortRows = (list: TemplateRow[]) =>
  [...list].sort((a, b) => Number(b.isActive) - Number(a.isActive) || a.name.localeCompare(b.name))

// ---------- which template applies to the customer ----------

/** The invoice types templates are chosen for; the other kinds have a single template slot. */
const slots = computed(() =>
  props.kind === 'invoice'
    ? [{ label: 'Monthly invoices', type: 'Monthly' }, { label: 'One-time invoices', type: 'One-time' }]
    : [{ label: '', type: null as string | null }]
)

const sameSlot = (row: TemplateRow, type: string | null) => type === null || typeLabel(row.invoiceType) === type

function overridesShared(row: TemplateRow): boolean {
  return !isShared.value && row.isActive && sharedRows.value.some(s => s.isActive && sameSlot(s, props.kind === 'invoice' ? typeLabel(row.invoiceType) : null))
}

const applies = computed((): AppliesItem[] => {
  if (isShared.value) return []
  // Nothing to explain when there are no shared templates: the customer's own are all there is
  if (sharedRows.value.length === 0) return []

  return slots.value.map(({ label, type }) => {
    const own = rows.value.find(r => r.isActive && sameSlot(r, type))
    const shared = sharedRows.value.find(r => r.isActive && sameSlot(r, type))

    if (own) {
      return {
        label,
        tone: 'ok' as const,
        text: shared
          ? `uses its own template “${own.name}”, overriding the shared “${shared.name}”.`
          : `uses its own template “${own.name}”.`
      }
    }
    if (shared) return { label, tone: 'ok' as const, text: `uses the shared template “${shared.name}”.` }

    return {
      label,
      tone: 'warn' as const,
      text: props.kind === 'email'
        ? 'no active template: the built-in text is used.'
        : 'no active template, so this can’t be generated yet.'
    }
  })
})

// ---------- loading and actions ----------

async function loadKind(customerId: number | null): Promise<TemplateRow[]> {
  if (customerId === null) {
    return props.kind === 'invoice'
      ? await templatesApi.getShared()
      : props.kind === 'email'
        ? await emailTemplatesApi.getShared()
        : await monthlyReportTemplatesApi.getShared()
  }
  return props.kind === 'invoice'
    ? await templatesApi.getByCustomerId(customerId)
    : props.kind === 'email'
      ? await emailTemplatesApi.getByCustomer(customerId)
      : await monthlyReportTemplatesApi.getByCustomer(customerId)
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const [own, shared] = await Promise.all([
      loadKind(props.customerId),
      isShared.value ? Promise.resolve([]) : loadKind(null)
    ])
    rows.value = sortRows(own)
    sharedRows.value = sortRows(shared)
  } catch (error) {
    loadError.value = 'The templates could not be loaded.'
    toast.failure('Could not load templates', error)
  } finally {
    loading.value = false
  }
}

async function setActive(row: TemplateRow, active: boolean) {
  busyId.value = row.id
  try {
    const api = apiFor(props.kind)
    await (active ? api.activate(row.id) : api.deactivate(row.id))
    toast.success(active ? 'Template activated' : 'Template deactivated')
    await load() // activating one can deactivate another of the same type
  } catch (error) {
    toast.failure(active ? 'Could not activate the template' : 'Could not deactivate the template', error)
  } finally {
    busyId.value = null
  }
}

/** Copies a shared template for this customer and makes the copy the one in use, then opens it. */
async function override(shared: TemplateRow) {
  const customerId = props.customerId
  if (customerId === null) return

  busyId.value = shared.id
  try {
    const name = shared.name
    const created: { id: number } = props.kind === 'invoice'
      ? await templatesApi.create({ customerId, invoiceType: shared.invoiceType as never, name, content: shared.content ?? '' })
      : props.kind === 'email'
        ? await emailTemplatesApi.create({ customerId, name, subject: shared.subject ?? '', body: shared.body ?? '' })
        : await monthlyReportTemplatesApi.create({ customerId, name, content: shared.content ?? '', invoiceType: shared.invoiceType as never })

    await apiFor(props.kind).activate(created.id)
    toast.success('This customer now has its own copy', { message: 'Change it here; the shared template is untouched.' })
    await router.push(editorLink(created.id))
  } catch (error) {
    toast.failure('Could not override the shared template', error)
    await load()
  } finally {
    busyId.value = null
  }
}

async function remove(row: TemplateRow) {
  const label = row.name || 'this template'
  const sharedNote = isShared.value ? ' Customers that used it will fall back to their own template, or none.' : ''
  if (!(await confirm({
    title: 'Delete template?',
    message: (props.kind === 'email'
      ? `“${label}” will be permanently deleted. Drafts already created in Gmail are not affected.`
      : `“${label}” will be permanently deleted. Invoices already generated keep their PDFs.`) + sharedNote,
    confirmLabel: 'Delete',
    tone: 'danger'
  }))) return

  try {
    await apiFor(props.kind).delete(row.id)
    toast.success('Template deleted')
    await load()
  } catch (error) {
    toast.failure('Could not delete the template', error)
  }
}

onMounted(load)
watch(() => props.customerId, load)
</script>

<style scoped>
.template-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.list-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
}

.list-header h3 {
  font-size: 1rem;
}

.help {
  margin: 0.2rem 0 0;
  max-width: 65ch;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.name {
  color: var(--color-text);
}

.name:hover {
  color: var(--color-primary);
}

.applies {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  margin: 0;
  padding: 0;
  list-style: none;
}

.applies li {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.5rem 0.75rem;
  border-radius: var(--radius-md);
  background: var(--color-surface-muted);
  font-size: var(--text-md);
  color: var(--color-text-secondary);
}

.applies li .app-icon {
  flex: none;
  color: var(--color-primary);
}

.applies li strong {
  color: var(--color-text);
}

.applies li.warn {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}

.applies li.warn .app-icon {
  color: var(--color-warning);
}

.override-note {
  margin-left: 0.5rem;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.shared-block {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  padding-top: 0.5rem;
  border-top: 1px solid var(--color-border);
}

.shared-block h4 {
  margin: 0;
  font-size: 0.95rem;
}
</style>
