<template>
  <div class="dashboard">
    <PageHeader :title="greeting" :subtitle="today">
      <template #actions>
        <BaseButton icon="plus" to="/customers/new">New customer</BaseButton>
        <BaseButton variant="primary" icon="plus" to="/invoices/new">New invoice</BaseButton>
      </template>
    </PageHeader>

    <LoadingState v-if="loading && !loaded" label="Loading your figures…" />

    <EmptyState
      v-else-if="loaded && customersStore.customers.length === 0"
      icon="customers"
      title="Welcome to nInvoices"
      description="Start by adding a customer with their rate. Then pick a month, mark the days you worked, and the invoice and timesheet are generated for you."
    >
      <BaseButton variant="primary" icon="plus" to="/customers/new">Add your first customer</BaseButton>
    </EmptyState>

    <template v-else-if="loaded">
      <div class="tiles">
        <!-- the headline figure: the theme's hero gradient -->
        <router-link class="stat-tile hero" to="/invoices">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="coins" /></span><span class="stat-tile-label">Outstanding</span></span>
          <span class="stat-tile-value">{{ formatTotals(outstanding.totals) }}</span>
          <span class="stat-tile-note">{{ outstanding.count }} finalized or sent</span>
        </router-link>
        <router-link class="stat-tile" :style="{ '--accent': 'var(--kpi-2)' }" :to="{ path: '/invoices', query: { status: 'Paid' } }">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="paid" /></span><span class="stat-tile-label">Paid in {{ year }}</span></span>
          <span class="stat-tile-value">{{ formatTotals(paidThisYear.totals) }}</span>
          <span class="stat-tile-note">{{ paidThisYear.count }} {{ paidThisYear.count === 1 ? 'invoice' : 'invoices' }}</span>
        </router-link>
        <router-link class="stat-tile" :style="{ '--accent': 'var(--kpi-3)' }" to="/invoices">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="invoices" /></span><span class="stat-tile-label">Invoiced in {{ year }}</span></span>
          <span class="stat-tile-value">{{ formatTotals(invoicedThisYear.totals) }}</span>
          <span class="stat-tile-note">excluding cancelled</span>
        </router-link>
        <router-link class="stat-tile" :style="{ '--accent': 'var(--kpi-4)' }" to="/customers">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="customers" /></span><span class="stat-tile-label">Customers</span></span>
          <span class="stat-tile-value">{{ customersStore.customers.length }}</span>
          <span class="stat-tile-note">{{ billedCustomers }} billed this year</span>
        </router-link>
      </div>

      <div class="columns">
        <BasePanel title="Needs your attention" class="attention">
          <ul v-if="attention.length" class="attention-list">
            <li v-for="item in attention" :key="item.key">
              <span class="attention-icon" :class="item.tone"><AppIcon :name="item.icon" /></span>
              <span class="attention-text">
                <strong>{{ item.title }}</strong>
                <span>{{ item.detail }}</span>
              </span>
              <BaseButton size="sm" :variant="item.primary ? 'primary' : 'secondary'" :to="item.to">{{ item.action }}</BaseButton>
            </li>
          </ul>
          <p v-else class="all-clear">
            <AppIcon name="success" />
            Nothing waiting: every customer is invoiced for {{ lastMonthLabel }} and nothing is left in draft.
          </p>
        </BasePanel>

        <BasePanel :title="`Invoiced per month${chartCurrency ? ` · ${chartCurrency}` : ''}`" description="Issued invoices, excluding cancelled ones. Hover a month for details.">
          <MonthlyBarChart
            :points="monthlyPoints"
            :format="value => formatMoney(value, chartCurrency || 'EUR')"
            :label="`Amount invoiced per month over the last 12 months, in ${chartCurrency || 'EUR'}`"
          />
          <p v-if="otherCurrencies.length" class="chart-note">
            Invoices in {{ otherCurrencies.join(', ') }} aren't included in this chart.
          </p>
        </BasePanel>
      </div>

      <BasePanel title="Recent invoices" flush>
        <template #actions>
          <BaseButton size="sm" variant="ghost" to="/invoices">All invoices</BaseButton>
        </template>
        <EmptyState
          v-if="recent.length === 0"
          icon="invoices"
          title="No invoices yet"
          description="Your latest invoices will show up here."
          compact
          class="flush-empty"
        />
        <div v-else class="table-wrap">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Invoice</th>
                <th scope="col">Customer</th>
                <th scope="col">Issued</th>
                <th scope="col">Status</th>
                <th scope="col" class="num">Total</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="invoice in recent" :key="invoice.id" class="clickable" @click="router.push(`/invoices/${invoice.id}`)">
                <td>
                  <router-link :to="`/invoices/${invoice.id}`" class="primary-cell number" @click.stop>{{ invoice.invoiceNumber }}</router-link>
                  <span class="sub">{{ invoice.month ? formatPeriod(invoice.month, invoice.year) : invoiceTypeLabel(invoice.type) }}</span>
                </td>
                <td>{{ customerName(invoice.customerId) }}</td>
                <td class="muted">{{ formatDate(invoice.issueDate) }}</td>
                <td><StatusPill :tone="invoiceStatus(invoice.status).tone">{{ invoiceStatus(invoice.status).label }}</StatusPill></td>
                <td class="num primary-cell">{{ formatMoney(invoice.total.amount, invoice.total.currency) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </BasePanel>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, type RouteLocationRaw } from 'vue-router'
import { useCustomersStore } from '@/stores/customers'
import { useInvoicesStore } from '@/stores/invoices'
import { useAuthStore } from '@/stores/auth'
import type { InvoiceDto } from '@/types'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import AppIcon, { type IconName } from '@/components/ui/AppIcon.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import MonthlyBarChart, { type MonthPoint } from '@/components/dashboard/MonthlyBarChart.vue'
import { useToast } from '@/composables/useToast'
import { formatMoney, formatDate, formatPeriod, invoiceStatus, invoiceTypeLabel } from '@/utils/format'

const router = useRouter()
const customersStore = useCustomersStore()
const invoicesStore = useInvoicesStore()
const authStore = useAuthStore()
const toast = useToast()

const loading = ref(false)
const loaded = ref(false)
const now = new Date()
const year = now.getFullYear()

const greeting = computed(() => {
  const hour = now.getHours()
  const part = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening'
  const first = (authStore.username || '').split(/[\s@.]/)[0]
  return first ? `${part}, ${first}` : part
})

const today = now.toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })

onMounted(async () => {
  loading.value = true
  try {
    await Promise.all([customersStore.fetchAll(), invoicesStore.fetchAll()])
    loaded.value = true
  } catch (error) {
    toast.failure('The dashboard could not load', error)
  } finally {
    loading.value = false
  }
})

const invoices = computed(() => invoicesStore.invoices)
const statusOf = (invoice: InvoiceDto) => invoiceStatus(invoice.status).name
const live = computed(() => invoices.value.filter(i => statusOf(i) !== 'Cancelled'))

function customerName(customerId: number): string {
  return customersStore.getCustomerById(customerId)?.name ?? 'Unknown customer'
}

// ---------- totals (kept per currency) ----------

function summarize(list: InvoiceDto[]) {
  const totals = new Map<string, number>()
  for (const invoice of list) {
    totals.set(invoice.total.currency, (totals.get(invoice.total.currency) ?? 0) + invoice.total.amount)
  }
  return { count: list.length, totals }
}

function formatTotals(totals: Map<string, number>): string {
  if (totals.size === 0) return formatMoney(0, 'EUR')
  return [...totals.entries()].map(([currency, amount]) => formatMoney(amount, currency)).join(' + ')
}

const thisYear = (i: InvoiceDto) => i.issueDate.startsWith(String(year))
const outstanding = computed(() => summarize(invoices.value.filter(i => ['Finalized', 'Sent'].includes(statusOf(i)))))
const paidThisYear = computed(() => summarize(invoices.value.filter(i => statusOf(i) === 'Paid' && thisYear(i))))
const invoicedThisYear = computed(() => summarize(live.value.filter(thisYear)))
const billedCustomers = computed(() => new Set(live.value.filter(thisYear).map(i => i.customerId)).size)

// ---------- chart: last 12 months in the most used currency ----------

const chartCurrency = computed(() => {
  const counts = new Map<string, number>()
  live.value.forEach(i => counts.set(i.total.currency, (counts.get(i.total.currency) ?? 0) + 1))
  return [...counts.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] ?? ''
})

const otherCurrencies = computed(() =>
  [...new Set(live.value.map(i => i.total.currency))].filter(c => c !== chartCurrency.value)
)

const monthlyPoints = computed((): MonthPoint[] => {
  return Array.from({ length: 12 }, (_, i) => {
    const date = new Date(year, now.getMonth() - 11 + i, 1)
    const key = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
    const inMonth = live.value.filter(inv => inv.issueDate.startsWith(key) && inv.total.currency === chartCurrency.value)
    return {
      key,
      label: date.toLocaleDateString(undefined, { month: 'long', year: 'numeric' }),
      short: date.toLocaleDateString(undefined, { month: 'short' }),
      value: inMonth.reduce((sum, inv) => sum + inv.total.amount, 0),
      count: inMonth.length
    }
  })
})

// ---------- what needs doing ----------

const lastMonth = new Date(year, now.getMonth() - 1, 1)
const lastMonthLabel = lastMonth.toLocaleDateString(undefined, { month: 'long', year: 'numeric' })

interface AttentionItem {
  key: string
  icon: IconName
  tone: 'info' | 'warning' | 'neutral'
  title: string
  detail: string
  action: string
  to: RouteLocationRaw
  primary?: boolean
}

const attention = computed((): AttentionItem[] => {
  const items: AttentionItem[] = []

  // Customers billed monthly before but without an invoice for last month
  const monthlyCustomers = new Set(
    live.value.filter(i => invoiceTypeLabel(i.type) === 'Monthly').map(i => i.customerId)
  )
  for (const customerId of monthlyCustomers) {
    const invoiced = live.value.some(i =>
      i.customerId === customerId && i.month === lastMonth.getMonth() + 1 && i.year === lastMonth.getFullYear())
    if (!invoiced) {
      items.push({
        key: `missing-${customerId}`,
        icon: 'calendar',
        tone: 'info',
        title: `${customerName(customerId)}: ${lastMonthLabel} not invoiced`,
        detail: 'Monthly customer with no invoice for last month yet.',
        action: 'Create invoice',
        to: { path: '/invoices/new', query: { customerId } },
        primary: true
      })
    }
  }

  const drafts = invoices.value.filter(i => statusOf(i) === 'Draft')
  if (drafts.length) {
    items.push({
      key: 'drafts',
      icon: 'lock',
      tone: 'neutral',
      title: drafts.length === 1 ? `${drafts[0].invoiceNumber} is still a draft` : `${drafts.length} invoices are still drafts`,
      detail: 'Finalize them once the amounts are right.',
      action: drafts.length === 1 ? 'Open' : 'Review',
      to: drafts.length === 1 ? `/invoices/${drafts[0].id}` : { path: '/invoices', query: { status: 'Draft' } }
    })
  }

  const toSend = invoices.value.filter(i => statusOf(i) === 'Finalized')
  if (toSend.length) {
    items.push({
      key: 'to-send',
      icon: 'send',
      tone: 'info',
      title: toSend.length === 1 ? `${toSend[0].invoiceNumber} is ready to send` : `${toSend.length} invoices are ready to send`,
      detail: 'Finalized but not marked as sent.',
      action: 'Review',
      to: { path: '/invoices', query: { status: 'Finalized' } }
    })
  }

  const waiting = invoices.value.filter(i => statusOf(i) === 'Sent')
  if (waiting.length) {
    const oldest = [...waiting].sort((a, b) => a.issueDate.localeCompare(b.issueDate))[0]
    const days = Math.floor((now.getTime() - new Date(`${oldest.issueDate}T00:00:00`).getTime()) / 86_400_000)
    items.push({
      key: 'awaiting',
      icon: 'paid',
      tone: days > 30 ? 'warning' : 'neutral',
      title: `${waiting.length} awaiting payment: ${formatTotals(summarize(waiting).totals)}`,
      detail: `Oldest issued ${days} ${days === 1 ? 'day' : 'days'} ago (${oldest.invoiceNumber}).`,
      action: 'Review',
      to: { path: '/invoices', query: { status: 'Sent' } }
    })
  }

  return items
})

const recent = computed(() =>
  [...invoices.value].sort((a, b) => b.issueDate.localeCompare(a.issueDate) || b.id - a.id).slice(0, 6)
)
</script>

<style scoped>
.dashboard {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 82rem;
}

.dashboard > :deep(.page-header) {
  margin-bottom: 0.25rem;
}

.tiles {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
  gap: 0.75rem;
}

.columns {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1.35fr);
  gap: 1rem;
  align-items: start;
}

@media (max-width: 1000px) {
  .columns {
    grid-template-columns: minmax(0, 1fr);
  }
}

.attention-list {
  display: flex;
  flex-direction: column;
  margin: -0.35rem 0;
  padding: 0;
  list-style: none;
}

.attention-list li {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.7rem 0;
  border-bottom: 1px solid var(--color-border);
}

.attention-list li:last-child {
  border-bottom: 0;
}

.attention-icon {
  width: 2rem;
  height: 2rem;
  flex: none;
  display: grid;
  place-items: center;
  border-radius: var(--radius-md);
  background: var(--color-surface-sunken);
  color: var(--color-text-muted);
}

.attention-icon.info {
  background: var(--color-primary-soft);
  color: var(--color-primary);
}

.attention-icon.warning {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}

.attention-text {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  font-size: var(--text-md);
}

.attention-text strong {
  color: var(--color-text);
  font-weight: 600;
}

.attention-text span {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.all-clear {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-md);
}

.all-clear .app-icon {
  color: var(--color-success);
}

.chart-note {
  margin: 0.5rem 0 0;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.flush-empty {
  border: 0;
  border-radius: 0 0 var(--radius-lg) var(--radius-lg);
}

.number {
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}

.number:hover {
  color: var(--color-primary);
}
</style>
