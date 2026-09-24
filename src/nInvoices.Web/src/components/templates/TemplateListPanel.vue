<template>
  <section class="template-list">
    <header class="list-header">
      <div>
        <h3>{{ kind === 'invoice' ? 'Invoice templates' : 'Monthly report templates' }}</h3>
        <p class="help">
          <template v-if="kind === 'invoice'">
            The active template for each invoice type is used when an invoice is generated.
          </template>
          <template v-else>
            The active template is used for the monthly timesheet PDF unless you pick another when generating.
          </template>
        </p>
      </div>
      <BaseButton variant="primary" icon="plus" :to="editorLink('new')">New template</BaseButton>
    </header>

    <div v-if="loading" class="state">Loading templates…</div>

    <div v-else-if="loadError" class="state">
      <p>{{ loadError }}</p>
      <BaseButton size="sm" @click="load">Try again</BaseButton>
    </div>

    <div v-else-if="rows.length === 0" class="empty">
      <AppIcon name="template" class="empty-icon" />
      <strong>No templates yet</strong>
      <span>Start from the sample and adjust it to your layout; the preview updates as you type.</span>
      <BaseButton variant="primary" :to="editorLink('new')">Create the first template</BaseButton>
    </div>

    <div v-else class="table-wrap">
      <table>
        <thead>
          <tr>
            <th scope="col">Name</th>
            <th v-if="kind === 'invoice'" scope="col">Invoice type</th>
            <th scope="col">Status</th>
            <th scope="col">Last changed</th>
            <th scope="col" class="actions-col"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.id">
            <td>
              <router-link :to="editorLink(row.id)" class="name">
                {{ row.name || `Untitled ${typeLabel(row.invoiceType).toLowerCase()} template` }}
              </router-link>
            </td>
            <td v-if="kind === 'invoice'">{{ typeLabel(row.invoiceType) }}</td>
            <td>
              <span class="pill" :class="row.isActive ? 'active' : 'inactive'">{{ row.isActive ? 'Active' : 'Inactive' }}</span>
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
                variant="ghost"
                icon="trash"
                icon-only
                class="danger-ghost"
                :aria-label="`Delete ${row.name || 'template'}`"
                title="Delete"
                @click="remove(row)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import { templatesApi } from '@/api/templates'
import { monthlyReportTemplatesApi } from '@/api/monthlyReportTemplates'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import type { TemplateKind } from './editor/templateVariables'

interface TemplateRow {
  id: number
  name: string
  invoiceType: unknown
  isActive: boolean
  createdAt: string
  updatedAt?: string
}

const props = defineProps<{
  kind: TemplateKind
  customerId: number
}>()

const toast = useToast()
const { confirm } = useConfirm()

const rows = ref<TemplateRow[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const busyId = ref<number | null>(null)

function editorLink(id: number | 'new') {
  const segment = props.kind === 'invoice' ? 'invoice' : 'monthly-report'
  return `/customers/${props.customerId}/templates/${segment}/${id}`
}

function typeLabel(type: unknown): string {
  const value = String(type)
  return value === 'OneTime' || value === '1' ? 'One-time' : value === 'Quarterly' ? 'Quarterly' : value === 'Annual' ? 'Annual' : 'Monthly'
}

function formatDate(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? ''
    : date.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const list: TemplateRow[] = props.kind === 'invoice'
      ? await templatesApi.getByCustomerId(props.customerId)
      : await monthlyReportTemplatesApi.getByCustomer(props.customerId)
    rows.value = [...list].sort((a, b) => Number(b.isActive) - Number(a.isActive) || a.name.localeCompare(b.name))
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
    const api = props.kind === 'invoice' ? templatesApi : monthlyReportTemplatesApi
    await (active ? api.activate(row.id) : api.deactivate(row.id))
    toast.success(active ? 'Template activated' : 'Template deactivated')
    await load() // activating one can deactivate another of the same type
  } catch (error) {
    toast.failure(active ? 'Could not activate the template' : 'Could not deactivate the template', error)
  } finally {
    busyId.value = null
  }
}

async function remove(row: TemplateRow) {
  const label = row.name || 'this template'
  if (!(await confirm({
    title: 'Delete template?',
    message: `“${label}” will be permanently deleted. Invoices already generated keep their PDFs.`,
    confirmLabel: 'Delete',
    tone: 'danger'
  }))) return

  try {
    const api = props.kind === 'invoice' ? templatesApi : monthlyReportTemplatesApi
    await api.delete(row.id)
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
  max-width: 60ch;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.state {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 1rem 0;
  color: var(--color-text-muted);
  font-size: var(--text-md);
}

.state p {
  margin: 0;
}

.empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.4rem;
  padding: 2.5rem 1rem;
  border: 1px dashed var(--color-border-strong);
  border-radius: var(--radius-lg);
  text-align: center;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.empty strong {
  color: var(--color-text);
}

.empty .base-btn {
  margin-top: 0.5rem;
}

.empty-icon {
  width: 2rem;
  height: 2rem;
  color: var(--color-text-subtle);
}

.table-wrap {
  overflow-x: auto;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--text-md);
}

th {
  padding: 0.55rem 0.9rem;
  background: var(--color-surface-muted);
  border-bottom: 1px solid var(--color-border);
  text-align: left;
  font-size: var(--text-xs);
  font-weight: 600;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-text-muted);
  white-space: nowrap;
}

td {
  padding: 0.55rem 0.9rem;
  border-bottom: 1px solid var(--color-border);
  vertical-align: middle;
}

tbody tr:last-child td {
  border-bottom: 0;
}

tbody tr:hover td {
  background: var(--color-surface-muted);
}

.name {
  font-weight: 600;
  color: var(--color-text);
}

.name:hover {
  color: var(--color-primary);
}

.muted {
  color: var(--color-text-muted);
  white-space: nowrap;
}

.pill {
  font-size: var(--text-xs);
  font-weight: 600;
  padding: 0.1rem 0.55rem;
  border-radius: 999px;
  border: 1px solid;
  white-space: nowrap;
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

.actions-col {
  width: 1%;
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.25rem;
  white-space: nowrap;
}

.danger-ghost:hover:not(:disabled) {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}
</style>
