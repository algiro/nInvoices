<template>
  <div class="invoices-page">
    <PageHeader title="Invoices" :subtitle="subtitle">
      <template #actions>
        <BaseButton variant="primary" icon="plus" to="/invoices/new">New invoice</BaseButton>
      </template>
    </PageHeader>

    <div v-if="summary && summary.totalCount > 0" class="tiles">
      <!-- the headline figure: the theme's hero gradient -->
      <div class="stat-tile hero">
        <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="coins" /></span><span class="stat-tile-label">Outstanding</span></span>
        <span class="stat-tile-value">{{ formatTotals(summary.outstanding.totals) }}</span>
        <span class="stat-tile-note">{{ summary.outstanding.count }} finalized or sent</span>
      </div>
      <button type="button" class="stat-tile" :style="{ '--accent': 'var(--kpi-2)' }" @click="applyView({ status: 'Paid', year: String(currentYear) })">
        <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="paid" /></span><span class="stat-tile-label">Paid in {{ currentYear }}</span></span>
        <span class="stat-tile-value">{{ formatTotals(summary.paidThisYear.totals) }}</span>
        <span class="stat-tile-note">{{ summary.paidThisYear.count }} {{ summary.paidThisYear.count === 1 ? 'invoice' : 'invoices' }}</span>
      </button>
      <button type="button" class="stat-tile" :style="{ '--accent': 'var(--kpi-4)' }" @click="applyView({ status: 'Draft' })">
        <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="lock" /></span><span class="stat-tile-label">Drafts</span></span>
        <span class="stat-tile-value">{{ summary.drafts }}</span>
        <span class="stat-tile-note">not finalized yet</span>
      </button>
    </div>

    <div class="filters">
      <div class="filters-top">
        <div class="status-chips" role="group" aria-label="Filter by status">
          <button
            v-for="chip in statusChips"
            :key="chip.value"
            type="button"
            class="chip"
            :class="{ active: (params.status ?? '') === chip.value }"
            :aria-pressed="(params.status ?? '') === chip.value"
            @click="setQuery({ status: chip.value })"
          >
            {{ chip.label }}
            <span class="chip-count">{{ chip.count }}</span>
          </button>
        </div>

        <div class="views">
          <select
            id="invoice-saved-view"
            class="control"
            aria-label="Saved views"
            :value="currentView?.name ?? ''"
            @change="onViewSelected(($event.target as HTMLSelectElement).value)"
          >
            <option value="">{{ savedViews.views.value.length ? 'Saved views…' : 'No saved views' }}</option>
            <option v-for="view in savedViews.views.value" :key="view.name" :value="view.name">{{ view.name }}</option>
          </select>
          <BaseButton
            v-if="currentView"
            size="sm"
            variant="ghost-danger"
            icon="trash"
            icon-only
            :aria-label="`Delete the view ${currentView.name}`"
            title="Delete this view"
            @click="deleteView(currentView.name)"
          />
          <BaseButton v-else size="sm" variant="ghost" icon="plus" :disabled="!hasFilters" @click="openSaveView">Save view</BaseButton>
        </div>
      </div>

      <div class="filter-controls">
        <div class="search">
          <AppIcon name="search" class="search-icon" />
          <input
            id="invoice-search"
            v-model="searchText"
            type="search"
            class="control"
            placeholder="Invoice number or customer"
            aria-label="Search invoices"
          />
        </div>
        <select id="invoice-customer-filter" class="control" aria-label="Filter by customer" :value="params.customerId ?? 0" @change="setQuery({ customerId: selectValue($event) })">
          <option :value="0">All customers</option>
          <option v-for="c in sortedCustomers" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
        <select id="invoice-type-filter" class="control" aria-label="Filter by type" :value="params.type ?? ''" @change="setQuery({ type: selectValue($event) })">
          <option value="">All types</option>
          <option value="Monthly">Monthly</option>
          <option value="OneTime">One-time</option>
        </select>
        <select id="invoice-year-filter" class="control" aria-label="Filter by year issued" :value="params.year ?? 0" @change="setQuery({ year: selectValue($event) })">
          <option :value="0">All years</option>
          <option v-for="year in summary?.years ?? []" :key="year" :value="year">{{ year }}</option>
        </select>
        <BaseButton v-if="hasFilters" variant="ghost" size="sm" @click="clearFilters">Clear filters</BaseButton>
      </div>
    </div>

    <div v-if="selected.size > 0" class="bulk-bar" role="region" aria-label="Selected invoices">
      <div class="bulk-info">
        <strong>{{ selected.size }} selected</strong>
        <button v-if="canSelectAllMatching" type="button" class="link" :disabled="selectingAll" @click="selectAllMatching">
          {{ selectingAll ? 'Selecting…' : `Select all ${page?.totalCount} matching` }}
        </button>
        <button type="button" class="link" @click="selected.clear()">Clear selection</button>
      </div>
      <div class="bulk-actions">
        <BaseButton size="sm" icon="lock" :disabled="!!bulkBusy || eligible('finalize') === 0" :loading="bulkBusy === 'finalize'" @click="runBulk('finalize')">
          Finalize ({{ eligible('finalize') }})
        </BaseButton>
        <BaseButton size="sm" icon="send" :disabled="!!bulkBusy || eligible('mark-as-sent') === 0" :loading="bulkBusy === 'mark-as-sent'" @click="runBulk('mark-as-sent')">
          Mark as sent ({{ eligible('mark-as-sent') }})
        </BaseButton>
        <BaseButton size="sm" icon="paid" :disabled="!!bulkBusy || eligible('mark-as-paid') === 0" :loading="bulkBusy === 'mark-as-paid'" @click="runBulk('mark-as-paid')">
          Mark as paid ({{ eligible('mark-as-paid') }})
        </BaseButton>
        <span class="bulk-divider" aria-hidden="true"></span>
        <label class="check">
          <input v-model="includeTimesheets" type="checkbox" />
          With timesheets
        </label>
        <BaseButton size="sm" variant="primary" icon="download" :disabled="!!bulkBusy" :loading="bulkBusy === 'download'" @click="downloadSelected">
          Download PDFs
        </BaseButton>
      </div>
    </div>

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

      <nav class="pager" aria-label="Pages">
        <span class="muted">{{ rangeLabel }}</span>
        <div class="pager-controls">
          <label class="page-size">
            Rows
            <select class="control" :value="params.pageSize" @change="setQuery({ size: selectValue($event) === 25 ? undefined : selectValue($event) })">
              <option v-for="size in PAGE_SIZES" :key="size" :value="size">{{ size }}</option>
            </select>
          </label>
          <BaseButton size="sm" icon="arrowLeft" icon-only aria-label="Previous page" :disabled="page.page <= 1" @click="goToPage(page.page - 1)" />
          <template v-for="item in pageItems" :key="item.key">
            <span v-if="item.gap" class="gap" aria-hidden="true">…</span>
            <button
              v-else
              type="button"
              class="page-button"
              :class="{ active: item.page === page.page }"
              :aria-current="item.page === page.page ? 'page' : undefined"
              @click="goToPage(item.page!)"
            >
              {{ item.page }}
            </button>
          </template>
          <BaseButton size="sm" icon="chevronRight" icon-only aria-label="Next page" :disabled="page.page >= lastPage" @click="goToPage(page.page + 1)" />
        </div>
      </nav>
    </template>

    <BaseDialog :open="saveViewOpen" title="Save this view" description="Filters, sort order and rows per page, under a name. Saved in this browser." size="sm" @close="saveViewOpen = false">
      <form id="save-view-form" novalidate @submit.prevent="confirmSaveView">
        <BaseField label="Name" for="view-name" required>
          <input id="view-name" v-model="viewName" type="text" class="control" placeholder="e.g. Unpaid this year" maxlength="60" />
        </BaseField>
      </form>
      <template #footer>
        <BaseButton @click="saveViewOpen = false">Cancel</BaseButton>
        <BaseButton type="submit" form="save-view-form" variant="primary" :disabled="!viewName.trim()">Save view</BaseButton>
      </template>
    </BaseDialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useRoute, useRouter, type LocationQueryRaw } from 'vue-router'
import { useCustomersStore } from '@/stores/customers'
import { invoicesApi } from '@/api/invoices'
import type {
  BulkInvoiceChange,
  InvoiceDto,
  InvoicePageDto,
  InvoiceSearchParams,
  InvoiceSortField,
  InvoiceSummaryDto,
  MoneyDto
} from '@/types'
import PageHeader from '@/components/ui/PageHeader.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import BaseField from '@/components/ui/BaseField.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import ActionMenu, { type ActionMenuItem } from '@/components/ui/ActionMenu.vue'
import { useInvoiceActions, actionsFor, type InvoiceActionId } from '@/composables/useInvoiceActions'
import { useSavedViews } from '@/composables/useSavedViews'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import { formatMoney, formatDate, formatPeriod, invoiceStatus, invoiceTypeLabel } from '@/utils/format'

const route = useRoute()
const router = useRouter()
const customersStore = useCustomersStore()
const toast = useToast()
const { confirm } = useConfirm()
const { run, isBusy } = useInvoiceActions()
const savedViews = useSavedViews('ninvoices.invoiceViews')

const PAGE_SIZES = [25, 50, 100]
const SORT_FIELDS: InvoiceSortField[] = ['IssueDate', 'Number', 'Customer', 'Period', 'Status', 'Total']
const currentYear = new Date().getFullYear()

// ---------- state in the URL ----------
// Every filter, the sort order and the page live in the query string, so a view can be
// reloaded, linked to and saved. Defaults are left out of it.

const FILTER_KEYS = ['status', 'customerId', 'type', 'year', 'q', 'sort', 'dir', 'size']

function queryValue(key: string): string {
  const value = route.query[key]
  return typeof value === 'string' ? value : ''
}

const params = computed((): Required<Pick<InvoiceSearchParams, 'sort' | 'dir' | 'page' | 'pageSize'>> & InvoiceSearchParams => {
  const sort = queryValue('sort') as InvoiceSortField
  const size = Number(queryValue('size'))
  return {
    status: queryValue('status') || undefined,
    customerId: Number(queryValue('customerId')) || undefined,
    type: queryValue('type') || undefined,
    year: Number(queryValue('year')) || undefined,
    search: queryValue('q') || undefined,
    sort: SORT_FIELDS.includes(sort) ? sort : 'IssueDate',
    dir: queryValue('dir') === 'asc' ? 'asc' : 'desc',
    page: Math.max(1, Number(queryValue('page')) || 1),
    pageSize: PAGE_SIZES.includes(size) ? size : 25
  }
})

/** The filter part of the URL (no page), as saved in a view. */
const filterQuery = computed(() =>
  Object.fromEntries(FILTER_KEYS.map(key => [key, queryValue(key)]).filter(([, value]) => value !== '')) as Record<string, string>
)

const hasFilters = computed(() => ['status', 'customerId', 'type', 'year', 'q'].some(key => queryValue(key) !== ''))

/** Changes query values ('' / 0 / undefined remove them); any change but the page goes back to page 1. */
function setQuery(changes: Record<string, string | number | undefined>) {
  const next: LocationQueryRaw = { ...route.query }
  for (const [key, value] of Object.entries(changes)) {
    if (value === undefined || value === '' || value === 0) delete next[key]
    else next[key] = String(value)
  }
  if (!('page' in changes)) delete next.page
  router.replace({ query: next })
}

function selectValue(event: Event): number | string {
  const value = (event.target as HTMLSelectElement).value
  return /^\d+$/.test(value) ? Number(value) : value
}

function applyView(query: Record<string, string>) {
  router.replace({ query })
}

function clearFilters() {
  searchText.value = ''
  applyView({})
}

// ---------- search box: typed text reaches the URL after a pause ----------

const searchText = ref(queryValue('q'))
let searchTimer: ReturnType<typeof setTimeout> | undefined

watch(searchText, text => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    if (text.trim() !== queryValue('q')) setQuery({ q: text.trim() })
  }, 300)
})

watch(() => queryValue('q'), value => {
  if (value !== searchText.value.trim()) searchText.value = value
})

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
  } catch (error: any) {
    if (id === requestId) loadError.value = error?.message ?? 'Unknown error'
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

// ---------- presentation ----------

const sortedCustomers = computed(() => [...customersStore.customers].sort((a, b) => a.name.localeCompare(b.name)))

function customerName(customerId: number): string {
  return customersStore.getCustomerById(customerId)?.name ?? '…'
}

const statusChips = computed(() => {
  const counts = page.value?.statusCounts ?? {}
  const all = Object.values(counts).reduce((sum, n) => sum + n, 0)
  return [
    { value: '', label: 'All', count: all },
    ...['Draft', 'Finalized', 'Sent', 'Paid', 'Cancelled'].map(name => ({
      value: name,
      label: invoiceStatus(name).label,
      count: counts[name] ?? 0
    }))
  ]
})

const lastPage = computed(() => Math.max(1, Math.ceil((page.value?.totalCount ?? 0) / params.value.pageSize)))

const rangeLabel = computed(() => {
  if (!page.value || page.value.totalCount === 0) return ''
  const first = (page.value.page - 1) * page.value.pageSize + 1
  const last = first + page.value.items.length - 1
  return `${first}–${last} of ${page.value.totalCount}`
})

const subtitle = computed(() => {
  if (!summary.value) return ''
  const total = summary.value.totalCount
  if (total === 0) return 'Nothing invoiced yet'
  const matching = page.value?.totalCount ?? total
  return matching === total ? `${total} ${total === 1 ? 'invoice' : 'invoices'}` : `${matching} of ${total} match`
})

/** Page numbers around the current one, with the first and last always shown. */
const pageItems = computed(() => {
  const current = page.value?.page ?? 1
  const pages = new Set([1, lastPage.value, current - 1, current, current + 1].filter(p => p >= 1 && p <= lastPage.value))
  const sorted = [...pages].sort((a, b) => a - b)
  const items: { key: string; page?: number; gap?: boolean }[] = []
  sorted.forEach((p, i) => {
    if (i > 0 && p - sorted[i - 1] > 1) items.push({ key: `gap-${p}`, gap: true })
    items.push({ key: `p-${p}`, page: p })
  })
  return items
})

function goToPage(target: number) {
  setQuery({ page: target === 1 ? undefined : target })
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

// Money totals are kept per currency; adding EUR to USD would be meaningless
function formatTotals(totals: MoneyDto[]): string {
  if (totals.length === 0) return formatMoney(0, 'EUR')
  return totals.map(t => formatMoney(t.amount, t.currency)).join(' + ')
}

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

// ---------- selection ----------
// Kept across pages and sort orders; a different filter starts a new selection.

const selected = reactive(new Map<number, InvoiceDto>())

watch(() => JSON.stringify([params.value.status, params.value.customerId, params.value.type, params.value.year, params.value.search]), () => selected.clear())

const pageAllSelected = computed(() => !!page.value?.items.length && page.value.items.every(i => selected.has(i.id)))
const pageSomeSelected = computed(() => !!page.value?.items.some(i => selected.has(i.id)))

function toggle(invoice: InvoiceDto) {
  if (selected.has(invoice.id)) selected.delete(invoice.id)
  else selected.set(invoice.id, invoice)
}

function togglePage() {
  const items = page.value?.items ?? []
  if (pageAllSelected.value) items.forEach(i => selected.delete(i.id))
  else items.forEach(i => selected.set(i.id, i))
}

const MAX_SELECT_ALL = 500
const selectingAll = ref(false)
const canSelectAllMatching = computed(() =>
  pageAllSelected.value && !!page.value && page.value.totalCount > selected.size && page.value.totalCount <= MAX_SELECT_ALL)

async function selectAllMatching() {
  selectingAll.value = true
  try {
    const pageSize = 200
    for (let p = 1; p <= Math.ceil((page.value?.totalCount ?? 0) / pageSize); p++) {
      const result = await invoicesApi.search({ ...params.value, page: p, pageSize })
      result.items.forEach(i => selected.set(i.id, i))
    }
  } catch (error) {
    toast.failure('Could not select all the invoices', error)
  } finally {
    selectingAll.value = false
  }
}

// ---------- bulk actions ----------

const BULK_RULES: Record<BulkInvoiceChange, { statuses: string[]; verb: string; done: string }> = {
  finalize: { statuses: ['Draft'], verb: 'Finalize', done: 'finalized' },
  'mark-as-sent': { statuses: ['Finalized'], verb: 'Mark as sent', done: 'marked as sent' },
  'mark-as-paid': { statuses: ['Finalized', 'Sent'], verb: 'Mark as paid', done: 'marked as paid' }
}

/** How many selected invoices the change applies to (the server checks again). */
function eligible(change: BulkInvoiceChange): number {
  return [...selected.values()].filter(i => BULK_RULES[change].statuses.includes(invoiceStatus(i.status).name)).length
}

const bulkBusy = ref<BulkInvoiceChange | 'download' | null>(null)
const includeTimesheets = ref(false)

function plural(n: number) {
  return `${n} ${n === 1 ? 'invoice' : 'invoices'}`
}

async function runBulk(change: BulkInvoiceChange) {
  const rule = BULK_RULES[change]
  const count = eligible(change)
  const others = selected.size - count
  const confirmed = await confirm({
    title: `${rule.verb} ${plural(count)}?`,
    message: others > 0
      ? `${plural(others)} of the selection can't be ${rule.done} in ${others === 1 ? 'its' : 'their'} current status and will be left as ${others === 1 ? 'it is' : 'they are'}.`
      : undefined,
    confirmLabel: rule.verb
  })
  if (!confirmed) return

  bulkBusy.value = change
  try {
    const result = await invoicesApi.bulkChange(change, [...selected.keys()])
    if (result.succeeded.length) toast.success(`${plural(result.succeeded.length)} ${rule.done}`)
    if (result.skipped.length) {
      const examples = result.skipped.slice(0, 3).map(s => `${s.invoiceNumber ?? s.id}: ${s.reason}`).join('; ')
      toast.warning(`${plural(result.skipped.length)} left unchanged`, {
        message: examples + (result.skipped.length > 3 ? '; …' : '')
      })
    }
    selected.clear()
    await refresh()
  } catch (error) {
    toast.failure(`Could not ${rule.verb.toLowerCase()} the invoices`, error)
  } finally {
    bulkBusy.value = null
  }
}

const MAX_DOWNLOAD = 100

async function downloadSelected() {
  if (selected.size > MAX_DOWNLOAD) {
    toast.warning(`Select at most ${MAX_DOWNLOAD} invoices to download`, { message: 'The PDFs are rendered one by one; split larger downloads.' })
    return
  }

  bulkBusy.value = 'download'
  try {
    const { blob, fileName } = await invoicesApi.downloadZip([...selected.keys()], includeTimesheets.value)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName ?? 'Invoices.zip'
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
  } catch (error) {
    toast.failure('Could not download the PDFs', error)
  } finally {
    bulkBusy.value = null
  }
}

// ---------- row actions ----------

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

// ---------- saved views ----------

const currentView = computed(() => savedViews.find(filterQuery.value))
const saveViewOpen = ref(false)
const viewName = ref('')

function onViewSelected(name: string) {
  const view = savedViews.views.value.find(v => v.name === name)
  if (view) applyView(view.query)
}

function openSaveView() {
  viewName.value = ''
  saveViewOpen.value = true
  requestAnimationFrame(() => document.getElementById('view-name')?.focus())
}

function confirmSaveView() {
  if (!viewName.value.trim()) return
  savedViews.save(viewName.value, filterQuery.value)
  saveViewOpen.value = false
  toast.success(`View “${viewName.value.trim()}” saved`)
}

async function deleteView(name: string) {
  if (!(await confirm({ title: `Delete the view “${name}”?`, message: 'The invoices are not affected.', confirmLabel: 'Delete', tone: 'danger' }))) return
  savedViews.remove(name)
}
</script>

<style scoped>
.tiles {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
  gap: 0.75rem;
  margin-bottom: 1.25rem;
}

.filters {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.filters-top {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
}

.status-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.chip {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.3rem 0.7rem;
  border: 1px solid var(--color-border-strong);
  border-radius: 999px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
  font-weight: 500;
}

.chip:hover {
  border-color: var(--color-primary-line);
  color: var(--color-primary);
}

.chip.active {
  background: var(--color-primary);
  border-color: var(--color-primary);
  color: var(--color-on-primary);
}

.chip-count {
  font-size: var(--text-xs);
  font-weight: 600;
  opacity: 0.75;
  font-variant-numeric: tabular-nums;
}

.views {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.views .control {
  width: auto;
  min-width: 11rem;
}

.filter-controls {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.filter-controls > .control {
  width: auto;
  min-width: 9rem;
}

.search {
  position: relative;
  flex: 1 1 18rem;
  max-width: 26rem;
}

.search-icon {
  position: absolute;
  left: 0.7rem;
  top: 50%;
  width: 1rem;
  height: 1rem;
  transform: translateY(-50%);
  color: var(--color-text-subtle);
  pointer-events: none;
}

.search .control {
  padding-left: 2.1rem;
}

/* stays in view while scrolling a long page */
.bulk-bar {
  position: sticky;
  top: 0.5rem;
  z-index: 15;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  margin-bottom: 0.75rem;
  padding: 0.6rem 0.9rem;
  border: 1px solid var(--color-primary-line);
  border-radius: var(--radius-lg);
  background: var(--color-primary-soft);
  box-shadow: var(--shadow-sm);
}

.bulk-info,
.bulk-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.75rem;
  font-size: var(--text-md);
}

.bulk-divider {
  width: 1px;
  height: 1.4rem;
  background: var(--color-border-strong);
}

.link {
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font-size: var(--text-sm);
  text-decoration: underline;
  cursor: pointer;
}

.check {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: var(--text-sm);
  color: var(--color-text-secondary);
}

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

.pager {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  margin-top: 0.75rem;
  font-size: var(--text-sm);
}

.pager-controls {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.3rem;
}

.page-size {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  margin-right: 0.5rem;
  color: var(--color-text-muted);
}

.page-size .control {
  width: auto;
}

.page-button {
  min-width: 2rem;
  padding: 0.3rem 0.5rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
  cursor: pointer;
}

.page-button:hover {
  border-color: var(--color-border-strong);
  color: var(--color-text);
}

.page-button.active {
  background: var(--color-primary);
  border-color: var(--color-primary);
  color: var(--color-on-primary);
  font-weight: 600;
}

.gap {
  padding: 0 0.2rem;
  color: var(--color-text-subtle);
}
</style>
