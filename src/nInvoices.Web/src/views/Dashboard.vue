<template>
  <div class="dashboard">
    <PageHeader :title="greeting" :subtitle="today">
      <template #actions>
        <BaseButton icon="plus" to="/customers/new">{{ t('customers.new') }}</BaseButton>
        <BaseButton variant="primary" icon="plus" to="/invoices/new">{{ t('layout.newInvoice') }}</BaseButton>
      </template>
    </PageHeader>

    <LoadingState v-if="loading && !loaded" :label="t('dashboard.loading')" />

    <EmptyState
      v-else-if="loaded && customersStore.customers.length === 0"
      icon="customers"
      :title="t('dashboard.welcome')"
      :description="t('dashboard.welcomeDescription')"
    >
      <BaseButton variant="primary" icon="plus" to="/customers/new">{{ t('dashboard.addFirstCustomer') }}</BaseButton>
    </EmptyState>

    <template v-else-if="loaded">
      <div class="tiles">
        <!-- the headline figure: the theme's hero gradient -->
        <router-link class="stat-tile hero" to="/invoices">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="coins" /></span><span class="stat-tile-label">{{ t('invoices.summary.outstanding') }}</span></span>
          <span class="stat-tile-value">{{ formatTotals(outstanding.totals) }}</span>
          <span class="stat-tile-note">{{ t('invoices.summary.finalizedOrSent', { n: outstanding.count }) }}</span>
        </router-link>
        <router-link class="stat-tile" :style="{ '--accent': 'var(--kpi-2)' }" :to="{ path: '/invoices', query: { status: 'Paid' } }">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="paid" /></span><span class="stat-tile-label">{{ t('invoices.summary.paidIn', { year }) }}</span></span>
          <span class="stat-tile-value">{{ formatTotals(paidThisYear.totals) }}</span>
          <span class="stat-tile-note">{{ t('invoices.count', paidThisYear.count) }}</span>
        </router-link>
        <router-link class="stat-tile" :style="{ '--accent': 'var(--kpi-3)' }" to="/invoices">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="invoices" /></span><span class="stat-tile-label">{{ t('dashboard.invoicedIn', { year }) }}</span></span>
          <span class="stat-tile-value">{{ formatTotals(invoicedThisYear.totals) }}</span>
          <span class="stat-tile-note">{{ t('dashboard.excludingCancelled') }}</span>
        </router-link>
        <router-link class="stat-tile" :style="{ '--accent': 'var(--kpi-4)' }" to="/customers">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="customers" /></span><span class="stat-tile-label">{{ t('nav.customers') }}</span></span>
          <span class="stat-tile-value">{{ customersStore.customers.length }}</span>
          <span class="stat-tile-note">{{ t('dashboard.billedThisYear', { n: billedCustomers }) }}</span>
        </router-link>
      </div>

      <div class="columns">
        <BasePanel :title="t('dashboard.attention')" class="attention">
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
            {{ t('dashboard.allClear', { month: lastMonthLabel }) }}
          </p>
        </BasePanel>

        <BasePanel :title="chartCurrency ? t('dashboard.perMonthIn', { currency: chartCurrency }) : t('dashboard.perMonth')" :description="t('dashboard.chartDescription')">
          <MonthlyBarChart
            :points="monthlyPoints"
            :format="value => formatMoney(value, chartCurrency || 'EUR')"
            :label="t('dashboard.chartLabel', { currency: chartCurrency || 'EUR' })"
          />
          <p v-if="otherCurrencies.length" class="chart-note">
            {{ t('dashboard.otherCurrencies', { currencies: otherCurrencies.join(', ') }) }}
          </p>
        </BasePanel>
      </div>

      <BasePanel :title="t('dashboard.recent')" flush>
        <template #actions>
          <BaseButton size="sm" variant="ghost" to="/invoices">{{ t('dashboard.allInvoices') }}</BaseButton>
        </template>
        <EmptyState
          v-if="recent.length === 0"
          icon="invoices"
          :title="t('invoices.emptyTitle')"
          :description="t('dashboard.recentEmpty')"
          compact
          class="flush-empty"
        />
        <div v-else class="table-wrap">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">{{ t('invoices.columns.invoice') }}</th>
                <th scope="col">{{ t('customers.customer') }}</th>
                <th scope="col">{{ t('invoices.columns.issued') }}</th>
                <th scope="col">{{ t('invoices.columns.status') }}</th>
                <th scope="col" class="num">{{ t('invoices.columns.total') }}</th>
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
import { currentLanguage } from '@/i18n'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

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
  const part = hour < 12 ? 'morning' : hour < 18 ? 'afternoon' : 'evening'
  const first = (authStore.username || '').split(/[\s@.]/)[0]
  return first ? t(`dashboard.greeting.${part}Named`, { name: first }) : t(`dashboard.greeting.${part}`)
})

const today = computed(() => now.toLocaleDateString(currentLanguage(), { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }))

onMounted(async () => {
  loading.value = true
  try {
    await Promise.all([customersStore.fetchAll(), invoicesStore.fetchAll()])
    loaded.value = true
  } catch (error) {
    toast.failure(t('dashboard.loadFailed'), error)
  } finally {
    loading.value = false
  }
})

const invoices = computed(() => invoicesStore.invoices)
const statusOf = (invoice: InvoiceDto) => invoiceStatus(invoice.status).name
const live = computed(() => invoices.value.filter(i => statusOf(i) !== 'Cancelled'))

function customerName(customerId: number): string {
  return customersStore.getCustomerById(customerId)?.name ?? t('invoices.details.unknownCustomer')
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
      label: date.toLocaleDateString(currentLanguage(), { month: 'long', year: 'numeric' }),
      short: date.toLocaleDateString(currentLanguage(), { month: 'short' }),
      value: inMonth.reduce((sum, inv) => sum + inv.total.amount, 0),
      count: inMonth.length
    }
  })
})

// ---------- what needs doing ----------

const lastMonth = new Date(year, now.getMonth() - 1, 1)
const lastMonthLabel = computed(() => lastMonth.toLocaleDateString(currentLanguage(), { month: 'long', year: 'numeric' }))

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
    live.value.filter(i => String(i.type) === 'Monthly').map(i => i.customerId)
  )
  for (const customerId of monthlyCustomers) {
    const invoiced = live.value.some(i =>
      i.customerId === customerId && i.month === lastMonth.getMonth() + 1 && i.year === lastMonth.getFullYear())
    if (!invoiced) {
      items.push({
        key: `missing-${customerId}`,
        icon: 'calendar',
        tone: 'info',
        title: t('dashboard.notInvoiced', { customer: customerName(customerId), month: lastMonthLabel.value }),
        detail: t('dashboard.notInvoicedDetail'),
        action: t('dashboard.createInvoice'),
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
      title: drafts.length === 1 ? t('dashboard.stillDraft', { number: drafts[0].invoiceNumber }) : t('dashboard.stillDrafts', { n: drafts.length }),
      detail: t('dashboard.stillDraftDetail'),
      action: drafts.length === 1 ? t('dashboard.open') : t('dashboard.review'),
      to: drafts.length === 1 ? `/invoices/${drafts[0].id}` : { path: '/invoices', query: { status: 'Draft' } }
    })
  }

  const toSend = invoices.value.filter(i => statusOf(i) === 'Finalized')
  if (toSend.length) {
    items.push({
      key: 'to-send',
      icon: 'send',
      tone: 'info',
      title: toSend.length === 1 ? t('dashboard.readyToSend', { number: toSend[0].invoiceNumber }) : t('dashboard.readyToSendMany', { n: toSend.length }),
      detail: t('dashboard.readyToSendDetail'),
      action: t('dashboard.review'),
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
      title: t('dashboard.awaiting', { n: waiting.length, total: formatTotals(summarize(waiting).totals) }),
      detail: t('dashboard.oldestIssued', { number: oldest.invoiceNumber }, days),
      action: t('dashboard.review'),
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
