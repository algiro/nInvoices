<template>
  <section class="template-list">
    <header class="list-header">
      <div>
        <h3>{{ heading }}</h3>
        <p class="help">
          <template v-if="isShared">{{ t('templates.list.helpShared') }}</template>
          <template v-else-if="kind === 'invoice'">{{ t('templates.list.helpInvoice') }}</template>
          <template v-else-if="kind === 'email'">{{ t('templates.list.helpEmail') }}</template>
          <template v-else>{{ t('templates.list.helpMonthlyReport') }}</template>
        </p>
      </div>
      <BaseButton variant="primary" icon="plus" :to="editorLink('new')">{{ t('templates.list.new') }}</BaseButton>
    </header>

    <ul v-if="!isShared && !loading && !loadError && applies.length" class="applies" :aria-label="t('templates.list.inUse')">
      <li v-for="item in applies" :key="item.label" :class="item.tone">
        <AppIcon :name="item.tone === 'warn' ? 'alert' : 'template'" />
        <span><strong v-if="item.label">{{ item.label }}:</strong> {{ item.text }}</span>
      </li>
    </ul>

    <LoadingState v-if="loading" :label="t('templates.list.loading')" />

    <EmptyState v-else-if="loadError" icon="alert" :title="t('templates.list.loadFailed')" :description="loadError" compact>
      <BaseButton @click="load">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="rows.length === 0"
      icon="template"
      :title="!isShared && sharedRows.length ? t('templates.list.noneOwn') : t('templates.list.none')"
      :description="!isShared && sharedRows.length ? t('templates.list.noneOwnDescription') : t('templates.list.noneDescription')"
      compact
    >
      <BaseButton variant="primary" icon="plus" :to="editorLink('new')">
        {{ !isShared && sharedRows.length ? t('templates.list.createOwn') : t('templates.list.createFirst') }}
      </BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">{{ t('customers.name') }}</th>
            <th v-if="kind === 'invoice'" scope="col">{{ t('invoiceWizard.customerStep.invoiceType') }}</th>
            <th scope="col">{{ t('invoices.columns.status') }}</th>
            <th scope="col">{{ t('invoices.details.lastChanged') }}</th>
            <th scope="col" class="actions"><span class="sr-only">{{ t('common.actions') }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.id">
            <td>
              <router-link :to="editorLink(row.id)" class="primary-cell name">
                {{ row.name || t('templates.untitled', { type: typeLabel(row.invoiceType) }) }}
              </router-link>
            </td>
            <td v-if="kind === 'invoice'">{{ typeLabel(row.invoiceType) }}</td>
            <td>
              <StatusPill :tone="row.isActive ? 'success' : 'neutral'">{{ row.isActive ? t('common.active') : t('common.inactive') }}</StatusPill>
              <span v-if="overridesShared(row)" class="override-note">{{ t('templates.list.overridesShared') }}</span>
            </td>
            <td class="muted">{{ formatDate(row.updatedAt ?? row.createdAt) }}</td>
            <td class="actions">
              <BaseButton size="sm" variant="ghost" icon="edit" :to="editorLink(row.id)">{{ t('common.edit') }}</BaseButton>
              <BaseButton
                v-if="row.isActive"
                size="sm"
                variant="ghost"
                :loading="busyId === row.id"
                @click="setActive(row, false)"
              >
                {{ t('templates.list.deactivate') }}
              </BaseButton>
              <BaseButton
                v-else
                size="sm"
                variant="ghost"
                icon="check"
                :loading="busyId === row.id"
                @click="setActive(row, true)"
              >
                {{ t('templates.list.activate') }}
              </BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                :aria-label="t('common.deleteNamed', { name: row.name || t('templates.list.template') })"
                :title="t('common.delete')"
                @click="remove(row)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="!isShared && !loading && !loadError && sharedRows.length" class="shared-block">
      <h4>{{ t('routes.sharedTemplates') }}</h4>
      <p class="help">
        {{ t('templates.list.sharedHelp') }}
      </p>
      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">{{ t('customers.name') }}</th>
              <th v-if="kind === 'invoice'" scope="col">{{ t('invoiceWizard.customerStep.invoiceType') }}</th>
              <th scope="col">{{ t('invoices.columns.status') }}</th>
              <th scope="col" class="actions"><span class="sr-only">{{ t('common.actions') }}</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in sharedRows" :key="row.id">
              <td>
                <router-link :to="editorLink(row.id, true)" class="primary-cell name">
                  {{ row.name || t('templates.untitled', { type: typeLabel(row.invoiceType) }) }}
                </router-link>
              </td>
              <td v-if="kind === 'invoice'">{{ typeLabel(row.invoiceType) }}</td>
              <td>
                <StatusPill :tone="row.isActive ? 'success' : 'neutral'">{{ row.isActive ? t('common.active') : t('common.inactive') }}</StatusPill>
              </td>
              <td class="actions">
                <BaseButton size="sm" variant="ghost" icon="edit" :to="editorLink(row.id, true)">{{ t('templates.list.editShared') }}</BaseButton>
                <BaseButton
                  size="sm"
                  variant="secondary"
                  icon="plus"
                  :loading="busyId === row.id"
                  :title="t('templates.list.overrideHint')"
                  @click="override(row)"
                >
                  {{ t('templates.list.override') }}
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
import { formatDate, invoiceTypeLabel } from '@/utils/format'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface TemplateRow {
  id: number
  name: string
  invoiceType?: unknown
  isActive: boolean
  createdAt: string
  updatedAt?: string | null
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
  if (props.kind === 'invoice') return isShared.value ? t('templates.list.sharedInvoice') : t('customers.tabs.templates')
  if (props.kind === 'email') return isShared.value ? t('templates.list.sharedEmail') : t('customers.tabs.emails')
  return isShared.value ? t('templates.list.sharedMonthlyReport') : t('templates.list.monthlyReport')
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

/** The invoice type's name, e.g. 'OneTime' (older data may carry the enum's number). */
function typeName(type: unknown): string {
  const value = String(type)
  return value === 'OneTime' || value === '1' ? 'OneTime' : value === 'Quarterly' ? 'Quarterly' : value === 'Annual' ? 'Annual' : 'Monthly'
}

function typeLabel(type: unknown): string {
  return invoiceTypeLabel(typeName(type))
}

const sortRows = (list: TemplateRow[]) =>
  [...list].sort((a, b) => Number(b.isActive) - Number(a.isActive) || a.name.localeCompare(b.name))

// ---------- which template applies to the customer ----------

/** The invoice types templates are chosen for; the other kinds have a single template slot. */
const slots = computed(() =>
  props.kind === 'invoice'
    ? [{ label: t('templates.list.monthlyInvoices'), type: 'Monthly' }, { label: t('templates.list.oneTimeInvoices'), type: 'OneTime' }]
    : [{ label: '', type: null as string | null }]
)

const sameSlot = (row: TemplateRow, type: string | null) => type === null || typeName(row.invoiceType) === type

function overridesShared(row: TemplateRow): boolean {
  return !isShared.value && row.isActive && sharedRows.value.some(s => s.isActive && sameSlot(s, props.kind === 'invoice' ? typeName(row.invoiceType) : null))
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
          ? t('templates.list.usesOwnOverriding', { name: own.name, shared: shared.name })
          : t('templates.list.usesOwn', { name: own.name })
      }
    }
    if (shared) return { label, tone: 'ok' as const, text: t('templates.list.usesShared', { name: shared.name }) }

    return {
      label,
      tone: 'warn' as const,
      text: props.kind === 'email'
        ? t('templates.list.noActiveEmail')
        : t('templates.list.noActive')
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
    loadError.value = t('templates.list.loadFailedDetail')
    toast.failure(t('templates.list.loadFailed'), error)
  } finally {
    loading.value = false
  }
}

async function setActive(row: TemplateRow, active: boolean) {
  busyId.value = row.id
  try {
    const api = apiFor(props.kind)
    await (active ? api.activate(row.id) : api.deactivate(row.id))
    toast.success(active ? t('templates.list.activated') : t('templates.list.deactivated'))
    await load() // activating one can deactivate another of the same type
  } catch (error) {
    toast.failure(active ? t('templates.list.activateFailed') : t('templates.list.deactivateFailed'), error)
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
    toast.success(t('templates.list.overridden'), { message: t('templates.list.overriddenMessage') })
    await router.push(editorLink(created.id))
  } catch (error) {
    toast.failure(t('templates.list.overrideFailed'), error)
    await load()
  } finally {
    busyId.value = null
  }
}

async function remove(row: TemplateRow) {
  const name = row.name || t('templates.list.thisTemplate')
  const message = props.kind === 'email' ? t('templates.list.deleteEmailMessage', { name }) : t('templates.list.deleteMessage', { name })
  if (!(await confirm({
    title: t('templates.list.deleteTitle'),
    message: isShared.value ? `${message} ${t('templates.list.deleteSharedNote')}` : message,
    confirmLabel: t('common.delete'),
    tone: 'danger'
  }))) return

  try {
    await apiFor(props.kind).delete(row.id)
    toast.success(t('templates.list.deleted'))
    await load()
  } catch (error) {
    toast.failure(t('templates.list.deleteFailed'), error)
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
