<template>
  <div class="invoices-page">
    <PageHeader title="Invoices" :subtitle="subtitle">
      <template #actions>
        <BaseButton variant="primary" icon="plus" to="/invoices/new">New invoice</BaseButton>
      </template>
    </PageHeader>

    <InvoiceSummaryTiles v-if="summary && summary.totalCount > 0" :summary="summary" />

    <InvoiceListFilters :status-counts="page?.statusCounts ?? {}" :years="summary?.years ?? []" />

    <InvoiceBulkBar v-if="selected.size > 0" :selection="selection" :total-count="page?.totalCount ?? 0" :refresh="refresh" />

    <LoadingState v-if="!page && loading" label="Loading invoices…" />

    <EmptyState v-else-if="loadError" icon="alert" title="Invoices could not be loaded" :description="loadError">
      <BaseButton @click="refresh">Try again</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="summary && summary.totalCount === 0"
      icon="invoices"
      title="No invoices yet"
      description="Pick a customer and a month, mark the worked days, and the invoice and timesheet PDFs are generated for you."
    >
      <BaseButton variant="primary" icon="plus" to="/invoices/new">New invoice</BaseButton>
    </EmptyState>

    <EmptyState v-else-if="page && page.totalCount === 0" icon="search" title="No invoice matches these filters" compact>
      <BaseButton @click="clearFilters">Clear filters</BaseButton>
    </EmptyState>

    <template v-else-if="page">
      <div class="table-wrap" :class="{ refreshing: loading }">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col" class="select-cell">
                <input
                  type="checkbox"
                  :checked="pageAllSelected"
                  :indeterminate.prop="pageSomeSelected && !pageAllSelected"
                  aria-label="Select the invoices on this page"
                  @change="togglePage"
                />
              </th>
              <th v-for="column in columns" :key="column.field" scope="col" :class="column.class" :aria-sort="ariaSort(column.field)">
                <button type="button" class="sort" :class="{ active: params.sort === column.field }" @click="sortBy(column.field)">
                  {{ column.label }}
                  <span class="sort-arrow" aria-hidden="true">{{ sortArrow(column.field) }}</span>
                </button>
              </th>
              <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="invoice in page.items"
              :key="invoice.id"
              class="clickable"
              :class="{ selected: selected.has(invoice.id) }"
              @click="router.push(`/invoices/${invoice.id}`)"
            >
              <td class="select-cell" @click.stop>
                <input
                  type="checkbox"
                  :checked="selected.has(invoice.id)"
                  :aria-label="`Select ${invoice.invoiceNumber}`"
                  @change="toggle(invoice)"
                />
              </td>
              <td>
                <router-link :to="`/invoices/${invoice.id}`" class="primary-cell number" @click.stop>{{ invoice.invoiceNumber }}</router-link>
                <span class="sub">{{ invoiceTypeLabel(invoice.type) }}</span>
              </td>
              <td>{{ customerName(invoice.customerId) }}</td>
              <td>{{ invoice.month ? formatPeriod(invoice.month, invoice.year) : '—' }}</td>
              <td class="muted">{{ formatDate(invoice.issueDate) }}</td>
              <td><StatusPill :tone="invoiceStatus(invoice.status).tone">{{ invoiceStatus(invoice.status).label }}</StatusPill></td>
              <td class="num primary-cell">{{ formatMoney(invoice.total.amount, invoice.total.currency) }}</td>
              <td class="actions" @click.stop>
                <BaseButton
                  v-if="actionsFor(invoice).primary"
                  size="sm"
                  variant="ghost"
                  :icon="actionsFor(invoice).primary!.icon"
                  :loading="isBusy(invoice, actionsFor(invoice).primary!.id)"
                  @click="runRowAction(actionsFor(invoice).primary!.id, invoice)"
                >
                  {{ actionsFor(invoice).primary!.label }}
                </BaseButton>
                <ActionMenu :items="menuItems(invoice)" :label="`More actions for ${invoice.invoiceNumber}`" />
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <InvoicePager :page="page" />
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useCustomersStore } from '@/stores/customers'
import { invoicesApi } from '@/api/invoices'
import type { InvoiceDto, InvoicePageDto, InvoiceSortField, InvoiceSummaryDto } from '@/types'
import PageHeader from '@/components/ui/PageHeader.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import ActionMenu, { type ActionMenuItem } from '@/components/ui/ActionMenu.vue'
import InvoiceSummaryTiles from '@/components/invoices/list/InvoiceSummaryTiles.vue'
import InvoiceListFilters from '@/components/invoices/list/InvoiceListFilters.vue'
import InvoiceBulkBar from '@/components/invoices/list/InvoiceBulkBar.vue'
import InvoicePager from '@/components/invoices/list/InvoicePager.vue'
import { useInvoiceActions, actionsFor, type InvoiceActionId } from '@/composables/useInvoiceActions'
import { useInvoiceListQuery } from '@/composables/useInvoiceListQuery'
import { useInvoiceSelection } from '@/composables/useInvoiceSelection'
import { errorMessage, useToast } from '@/composables/useToast'
import { formatMoney, formatDate, formatPeriod, invoiceStatus, invoiceTypeLabel } from '@/utils/format'

const router = useRouter()
const customersStore = useCustomersStore()
const toast = useToast()
const { run, isBusy } = useInvoiceActions()
// Filters, sort order and page live in the URL (see useInvoiceListQuery)
const { params, setQuery, applyView } = useInvoiceListQuery()

// ---------- data ----------

const page = ref<InvoicePageDto | null>(null)
const summary = ref<InvoiceSummaryDto | null>(null)
const loading = ref(false)
const loadError = ref<string | null>(null)
let requestId = 0

async function loadPage() {
  const id = ++requestId
  loading.value = true
  try {
    const result = await invoicesApi.search(params.value)
    if (id !== requestId) return
    page.value = result
    loadError.value = null
    // The server moves a page past the end back to the last one; keep the URL in step
    if (result.page !== params.value.page) setQuery({ page: result.page === 1 ? undefined : result.page })
  } catch (error) {
    if (id === requestId) loadError.value = errorMessage(error, 'Unknown error')
  } finally {
    if (id === requestId) loading.value = false
  }
}

async function loadSummary() {
  try {
    summary.value = await invoicesApi.getSummary()
  } catch {
    // the tiles are a convenience; the list still works without them
  }
}

function refresh() {
  return Promise.all([loadPage(), loadSummary()])
}

watch(params, loadPage, { deep: true })

onMounted(() => {
  customersStore.fetchAll().catch(error => toast.failure('Could not load the customers', error))
  refresh()
})

function clearFilters() {
  applyView({})
}

// ---------- presentation ----------

function customerName(customerId: number): string {
  return customersStore.getCustomerById(customerId)?.name ?? '…'
}

const subtitle = computed(() => {
  if (!summary.value) return ''
  const total = summary.value.totalCount
  if (total === 0) return 'Nothing invoiced yet'
  const matching = page.value?.totalCount ?? total
  return matching === total ? `${total} ${total === 1 ? 'invoice' : 'invoices'}` : `${matching} of ${total} match`
})

// ---------- sorting ----------

const columns: { field: InvoiceSortField; label: string; class?: string }[] = [
  { field: 'Number', label: 'Invoice' },
  { field: 'Customer', label: 'Customer' },
  { field: 'Period', label: 'Period' },
  { field: 'IssueDate', label: 'Issued' },
  { field: 'Status', label: 'Status' },
  { field: 'Total', label: 'Total', class: 'num' }
]

// Dates and amounts read best newest / largest first; names and states A→Z / in lifecycle order
const DESCENDING_FIRST: InvoiceSortField[] = ['IssueDate', 'Period', 'Total']

function sortBy(field: InvoiceSortField) {
  const dir = params.value.sort === field
    ? (params.value.dir === 'desc' ? 'asc' : 'desc')
    : (DESCENDING_FIRST.includes(field) ? 'desc' : 'asc')
  setQuery({
    sort: field === 'IssueDate' ? undefined : field,
    dir: field === 'IssueDate' && dir === 'desc' ? undefined : dir
  })
}

function sortArrow(field: InvoiceSortField) {
  if (params.value.sort !== field) return ''
  return params.value.dir === 'asc' ? '↑' : '↓'
}

function ariaSort(field: InvoiceSortField) {
  if (params.value.sort !== field) return undefined
  return params.value.dir === 'asc' ? 'ascending' : 'descending'
}

// ---------- selection and row actions ----------

const selection = useInvoiceSelection(page, params)
const { selected, pageAllSelected, pageSomeSelected, toggle, togglePage } = selection

async function runRowAction(action: InvoiceActionId, invoice: InvoiceDto) {
  if (await run(action, invoice)) {
    selected.delete(invoice.id)
    await refresh()
  }
}

function menuItems(invoice: InvoiceDto): ActionMenuItem[] {
  return actionsFor(invoice).more.map(action =>
    action === 'separator'
      ? { separator: true }
      : { label: action.label, icon: action.icon, danger: action.danger, run: () => runRowAction(action.id, invoice) }
  )
}
</script>

<style scoped>
.table-wrap.refreshing {
  opacity: 0.6;
  transition: opacity 0.15s;
}

.select-cell {
  width: 2.25rem;
}

.select-cell input {
  width: 1rem;
  height: 1rem;
  cursor: pointer;
}

tr.selected td {
  background: var(--color-primary-soft);
}

.sort {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0;
  border: 0;
  background: none;
  color: inherit;
  font: inherit;
  text-transform: inherit;
  letter-spacing: inherit;
  cursor: pointer;
}

.sort:hover,
.sort.active {
  color: var(--color-text);
}

th.num .sort {
  flex-direction: row-reverse;
}

.sort-arrow {
  display: inline-block;
  min-width: 0.7rem;
}

.number {
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}

.number:hover {
  color: var(--color-primary);
}
</style>
