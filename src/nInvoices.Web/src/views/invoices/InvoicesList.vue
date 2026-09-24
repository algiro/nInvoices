<template>
  <div class="invoices-page">
    <PageHeader title="Invoices" :subtitle="subtitle">
      <template #actions>
        <BaseButton variant="primary" icon="plus" to="/invoices/new">New invoice</BaseButton>
      </template>
    </PageHeader>

    <div v-if="invoicesStore.invoices.length > 0" class="tiles">
      <div class="tile">
        <span class="tile-label">Outstanding</span>
        <span class="tile-value">{{ formatTotals(outstanding.totals) }}</span>
        <span class="tile-note">{{ outstanding.count }} finalized or sent</span>
      </div>
      <div class="tile">
        <span class="tile-label">Paid in {{ currentYear }}</span>
        <span class="tile-value">{{ formatTotals(paidThisYear.totals) }}</span>
        <span class="tile-note">{{ paidThisYear.count }} {{ paidThisYear.count === 1 ? 'invoice' : 'invoices' }}</span>
      </div>
      <div class="tile">
        <span class="tile-label">Drafts</span>
        <span class="tile-value">{{ statusCounts.Draft ?? 0 }}</span>
        <span class="tile-note">not finalized yet</span>
      </div>
    </div>

    <div class="filters">
      <div class="status-chips" role="group" aria-label="Filter by status">
        <button
          v-for="chip in statusChips"
          :key="chip.value"
          type="button"
          class="chip"
          :class="{ active: statusFilter === chip.value }"
          :aria-pressed="statusFilter === chip.value"
          @click="setStatus(chip.value)"
        >
          {{ chip.label }}
          <span class="chip-count">{{ chip.count }}</span>
        </button>
      </div>
      <div class="filter-controls">
        <div class="search">
          <AppIcon name="search" class="search-icon" />
          <input
            id="invoice-search"
            v-model="searchQuery"
            type="search"
            class="control"
            placeholder="Invoice number or customer"
            aria-label="Search invoices"
          />
        </div>
        <select id="invoice-customer-filter" v-model="customerFilter" class="control" aria-label="Filter by customer">
          <option :value="0">All customers</option>
          <option v-for="c in customersWithInvoices" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
        <select id="invoice-type-filter" v-model="typeFilter" class="control" aria-label="Filter by type">
          <option value="">All types</option>
          <option value="Monthly">Monthly</option>
          <option value="One-time">One-time</option>
        </select>
      </div>
    </div>

    <LoadingState v-if="invoicesStore.loading && invoicesStore.invoices.length === 0" label="Loading invoices…" />

    <EmptyState v-else-if="loadError" icon="alert" title="Invoices could not be loaded" :description="loadError">
      <BaseButton @click="loadData">Try again</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="invoicesStore.invoices.length === 0"
      icon="invoices"
      title="No invoices yet"
      description="Pick a customer and a month, mark the worked days, and the invoice and timesheet PDFs are generated for you."
    >
      <BaseButton variant="primary" icon="plus" to="/invoices/new">New invoice</BaseButton>
    </EmptyState>

    <EmptyState v-else-if="filteredInvoices.length === 0" icon="search" title="No invoice matches these filters" compact>
      <BaseButton @click="clearFilters">Clear filters</BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">Invoice</th>
            <th scope="col">Customer</th>
            <th scope="col">Period</th>
            <th scope="col">Issued</th>
            <th scope="col">Status</th>
            <th scope="col" class="num">Total</th>
            <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="invoice in filteredInvoices" :key="invoice.id" class="clickable" @click="router.push(`/invoices/${invoice.id}`)">
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
                @click="run(actionsFor(invoice).primary!.id, invoice)"
              >
                {{ actionsFor(invoice).primary!.label }}
              </BaseButton>
              <ActionMenu :items="menuItems(invoice)" :label="`More actions for ${invoice.invoiceNumber}`" />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useInvoicesStore } from '@/stores/invoices'
import { useCustomersStore } from '@/stores/customers'
import type { InvoiceDto } from '@/types'
import PageHeader from '@/components/ui/PageHeader.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import ActionMenu, { type ActionMenuItem } from '@/components/ui/ActionMenu.vue'
import { useInvoiceActions, actionsFor } from '@/composables/useInvoiceActions'
import { formatMoney, formatDate, formatPeriod, invoiceStatus, invoiceTypeLabel } from '@/utils/format'

const route = useRoute()
const router = useRouter()
const invoicesStore = useInvoicesStore()
const customersStore = useCustomersStore()
const { run, isBusy } = useInvoiceActions()

const loadError = ref<string | null>(null)
const searchQuery = ref('')
const customerFilter = ref(0)
const typeFilter = ref('')
const currentYear = new Date().getFullYear()

// The status filter lives in the URL (?status=Sent) so it survives reloads and can be linked to
const statusFilter = computed(() => (typeof route.query.status === 'string' ? route.query.status : ''))

function setStatus(value: string) {
  router.replace({ query: { ...route.query, status: value || undefined } })
}

function clearFilters() {
  searchQuery.value = ''
  customerFilter.value = 0
  typeFilter.value = ''
  setStatus('')
}

const statusCounts = computed(() => {
  const counts: Record<string, number> = {}
  for (const invoice of invoicesStore.invoices) {
    const name = invoiceStatus(invoice.status).name
    counts[name] = (counts[name] ?? 0) + 1
  }
  return counts
})

const statusChips = computed(() => [
  { value: '', label: 'All', count: invoicesStore.invoices.length },
  ...['Draft', 'Finalized', 'Sent', 'Paid', 'Cancelled'].map(name => ({
    value: name,
    label: invoiceStatus(name).label,
    count: statusCounts.value[name] ?? 0
  }))
])

const customersWithInvoices = computed(() => {
  const ids = new Set(invoicesStore.invoices.map(i => i.customerId))
  return customersStore.customers.filter(c => ids.has(c.id)).sort((a, b) => a.name.localeCompare(b.name))
})

function customerName(customerId: number): string {
  return customersStore.getCustomerById(customerId)?.name ?? 'Unknown customer'
}

const filteredInvoices = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  return invoicesStore.invoices
    .filter(invoice => !statusFilter.value || invoiceStatus(invoice.status).name === statusFilter.value)
    .filter(invoice => !customerFilter.value || invoice.customerId === customerFilter.value)
    .filter(invoice => !typeFilter.value || invoiceTypeLabel(invoice.type) === typeFilter.value)
    .filter(invoice =>
      !query ||
      invoice.invoiceNumber.toLowerCase().includes(query) ||
      customerName(invoice.customerId).toLowerCase().includes(query))
    .sort((a, b) => b.issueDate.localeCompare(a.issueDate) || b.id - a.id)
})

const subtitle = computed(() => {
  const total = invoicesStore.invoices.length
  const shown = filteredInvoices.value.length
  if (total === 0) return 'Nothing invoiced yet'
  return shown === total ? `${total} ${total === 1 ? 'invoice' : 'invoices'}` : `Showing ${shown} of ${total}`
})

// Money totals are kept per currency; adding EUR to USD would be meaningless
function summarize(invoices: InvoiceDto[]) {
  const totals = new Map<string, number>()
  for (const invoice of invoices) {
    totals.set(invoice.total.currency, (totals.get(invoice.total.currency) ?? 0) + invoice.total.amount)
  }
  return { count: invoices.length, totals }
}

function formatTotals(totals: Map<string, number>): string {
  if (totals.size === 0) return formatMoney(0, 'EUR')
  return [...totals.entries()].map(([currency, amount]) => formatMoney(amount, currency)).join(' + ')
}

const outstanding = computed(() =>
  summarize(invoicesStore.invoices.filter(i => ['Finalized', 'Sent'].includes(invoiceStatus(i.status).name)))
)

const paidThisYear = computed(() =>
  summarize(invoicesStore.invoices.filter(i =>
    invoiceStatus(i.status).name === 'Paid' && i.issueDate.startsWith(String(currentYear))))
)

function menuItems(invoice: InvoiceDto): ActionMenuItem[] {
  return actionsFor(invoice).more.map(action =>
    action === 'separator'
      ? { separator: true }
      : { label: action.label, icon: action.icon, danger: action.danger, run: () => run(action.id, invoice) }
  )
}

async function loadData() {
  loadError.value = null
  try {
    await Promise.all([invoicesStore.fetchAll(), customersStore.fetchAll()])
  } catch (error: any) {
    loadError.value = error?.message ?? 'Unknown error'
  }
}

onMounted(loadData)

// A customer link such as /invoices?customerId=3 preselects that customer
watch(() => route.query.customerId, value => {
  customerFilter.value = Number(value) || 0
}, { immediate: true })
</script>

<style scoped>
.tiles {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
  gap: 0.75rem;
  margin-bottom: 1.25rem;
}

.tile {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  padding: 0.9rem 1rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
}

.tile-label {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.tile-value {
  font-size: 1.35rem;
  font-weight: 650;
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
  overflow-wrap: anywhere;
}

.tile-note {
  font-size: var(--text-sm);
  color: var(--color-text-subtle);
}

.filters {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1rem;
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

.filter-controls {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.filter-controls > .control {
  width: auto;
  min-width: 11rem;
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

.number {
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}

.number:hover {
  color: var(--color-primary);
}
</style>
