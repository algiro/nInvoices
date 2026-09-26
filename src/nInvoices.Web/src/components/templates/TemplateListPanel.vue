<template>
  <section class="template-list">
    <header class="list-header">
      <div>
        <h3>{{ kind === 'invoice' ? 'Invoice templates' : kind === 'email' ? 'Email templates' : 'Monthly report templates' }}</h3>
        <p class="help">
          <template v-if="kind === 'invoice'">
            The active template for each invoice type is used when an invoice is generated.
          </template>
          <template v-else-if="kind === 'email'">
            Subject and text of the email created in Gmail from an invoice. The active one is preselected; without one, a built-in text is used.
          </template>
          <template v-else>
            The active template is used for the monthly timesheet PDF unless you pick another when generating.
          </template>
        </p>
      </div>
      <BaseButton variant="primary" icon="plus" :to="editorLink('new')">New template</BaseButton>
    </header>

    <LoadingState v-if="loading" label="Loading templates…" />

    <EmptyState v-else-if="loadError" icon="alert" title="Templates could not be loaded" :description="loadError" compact>
      <BaseButton @click="load">Try again</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="rows.length === 0"
      icon="template"
      title="No templates yet"
      description="Start from the sample and adjust it to your layout; the preview updates as you type."
      compact
    >
      <BaseButton variant="primary" icon="plus" :to="editorLink('new')">Create the first template</BaseButton>
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
  </section>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
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
  return `/customers/${props.customerId}/templates/${props.kind}/${id}`
}

/** The operations every template kind shares. */
function apiFor(kind: TemplateKind) {
  return kind === 'invoice' ? templatesApi : kind === 'email' ? emailTemplatesApi : monthlyReportTemplatesApi
}

function typeLabel(type: unknown): string {
  const value = String(type)
  return value === 'OneTime' || value === '1' ? 'One-time' : value === 'Quarterly' ? 'Quarterly' : value === 'Annual' ? 'Annual' : 'Monthly'
}


async function load() {
  loading.value = true
  loadError.value = null
  try {
    const list: TemplateRow[] = props.kind === 'invoice'
      ? await templatesApi.getByCustomerId(props.customerId)
      : props.kind === 'email'
        ? await emailTemplatesApi.getByCustomer(props.customerId)
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

async function remove(row: TemplateRow) {
  const label = row.name || 'this template'
  if (!(await confirm({
    title: 'Delete template?',
    message: props.kind === 'email'
      ? `“${label}” will be permanently deleted. Drafts already created in Gmail are not affected.`
      : `“${label}” will be permanently deleted. Invoices already generated keep their PDFs.`,
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
</style>
