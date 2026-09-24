<template>
  <div class="invoice-details">
    <LoadingState v-if="loading && !invoice" label="Loading invoice…" />

    <EmptyState v-else-if="error && !invoice" icon="alert" title="Invoice could not be loaded" :description="error">
      <BaseButton @click="loadData">Try again</BaseButton>
      <BaseButton variant="ghost" to="/invoices">Back to invoices</BaseButton>
    </EmptyState>

    <template v-else-if="invoice">
      <PageHeader :title="`Invoice ${invoice.invoiceNumber}`">
        <template #subtitle>
          <span class="meta">
            <StatusPill :tone="status.tone">{{ status.label }}</StatusPill>
            <router-link :to="`/customers/${invoice.customerId}`">{{ customerName }}</router-link>
            <span class="dot" aria-hidden="true">·</span>
            <span>{{ invoiceTypeLabel(invoice.type) }}{{ invoice.month ? `, ${formatPeriod(invoice.month, invoice.year)}` : '' }}</span>
          </span>
        </template>
        <template #actions>
          <BaseButton
            icon="download"
            :loading="isBusy(invoice, 'downloadPdf')"
            @click="run('downloadPdf', invoice)"
          >
            PDF
          </BaseButton>
          <BaseButton
            v-if="isMonthly(invoice)"
            icon="calendar"
            :loading="isBusy(invoice, 'downloadReport')"
            @click="run('downloadReport', invoice)"
          >
            Monthly report
          </BaseButton>
          <BaseButton
            v-if="actions.primary"
            variant="primary"
            :icon="actions.primary.icon"
            :loading="isBusy(invoice, actions.primary.id)"
            @click="runAndFollow(actions.primary.id)"
          >
            {{ actions.primary.label }}
          </BaseButton>
          <ActionMenu :items="menuItems" label="More invoice actions" />
        </template>
      </PageHeader>

      <ol v-if="status.name !== 'Cancelled'" class="lifecycle" aria-label="Invoice progress">
        <li
          v-for="(step, index) in lifecycle"
          :key="step"
          :class="{ done: index < stepIndex, current: index === stepIndex }"
          :aria-current="index === stepIndex ? 'step' : undefined"
        >
          <span class="marker"><AppIcon v-if="index < stepIndex" name="check" /><template v-else>{{ index + 1 }}</template></span>
          {{ step }}
        </li>
      </ol>
      <div v-else class="cancelled-note" role="status">
        <AppIcon name="ban" />
        This invoice was cancelled. It stays on record but isn't counted as outstanding.
      </div>

      <div class="layout">
        <TemplatePreviewPane
          class="document"
          title="Invoice document"
          :html="invoice.renderedContent ?? null"
          :loading="false"
          :stale="false"
          caption="As generated; regenerate from the ⋯ menu to apply template changes"
        />

        <div class="side">
          <BasePanel title="Amounts">
            <dl class="amounts">
              <div><dt>Subtotal</dt><dd>{{ money(invoice.subtotal) }}</dd></div>
              <div v-if="invoice.totalExpenses?.amount"><dt>Expenses</dt><dd>{{ money(invoice.totalExpenses) }}</dd></div>
              <div><dt>Taxes</dt><dd>{{ money(invoice.totalTaxes) }}</dd></div>
              <div class="total"><dt>Total</dt><dd>{{ money(invoice.total) }}</dd></div>
            </dl>
          </BasePanel>

          <BasePanel title="Details">
            <dl class="facts">
              <div><dt>Customer</dt><dd><router-link :to="`/customers/${invoice.customerId}`">{{ customerName }}</router-link></dd></div>
              <div><dt>Invoice number</dt><dd class="mono">{{ invoice.invoiceNumber }}</dd></div>
              <div><dt>Issue date</dt><dd>{{ formatDate(invoice.issueDate) }}</dd></div>
              <div><dt>Due date</dt><dd>{{ invoice.dueDate ? formatDate(invoice.dueDate) : 'Not set' }}</dd></div>
              <div v-if="invoice.month"><dt>Period</dt><dd>{{ formatPeriod(invoice.month, invoice.year) }}</dd></div>
              <div v-if="invoice.workedDays !== undefined && invoice.workedDays !== null"><dt>Worked days</dt><dd>{{ invoice.workedDays }}</dd></div>
              <div><dt>Created</dt><dd>{{ formatDate(invoice.createdAt) }}</dd></div>
              <div v-if="invoice.updatedAt && invoice.updatedAt !== invoice.createdAt"><dt>Last changed</dt><dd>{{ formatDate(invoice.updatedAt) }}</dd></div>
            </dl>
          </BasePanel>

          <BasePanel v-if="invoice.notes" title="Notes">
            <p class="notes">{{ invoice.notes }}</p>
          </BasePanel>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useInvoicesStore } from '@/stores/invoices'
import { useCustomersStore } from '@/stores/customers'
import type { MoneyDto } from '@/types'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import ActionMenu, { type ActionMenuItem } from '@/components/ui/ActionMenu.vue'
import TemplatePreviewPane from '@/components/templates/editor/TemplatePreviewPane.vue'
import { useInvoiceActions, actionsFor, isMonthly, type InvoiceActionId } from '@/composables/useInvoiceActions'
import { setPageTitle } from '@/composables/usePageTitle'
import { formatMoney, formatDate, formatPeriod, invoiceStatus, invoiceTypeLabel } from '@/utils/format'

const route = useRoute()
const router = useRouter()
const invoicesStore = useInvoicesStore()
const customersStore = useCustomersStore()
const { run, isBusy } = useInvoiceActions()

const invoiceId = computed(() => Number(route.params.id))
// The store may still hold the previously opened invoice
const invoice = computed(() =>
  invoicesStore.selectedInvoice?.id === invoiceId.value ? invoicesStore.selectedInvoice : null
)
const loading = ref(false)
const error = ref<string | null>(null)

const status = computed(() => invoiceStatus(invoice.value?.status ?? 'Draft'))
const actions = computed(() => (invoice.value ? actionsFor(invoice.value) : { primary: null, more: [] }))

const lifecycle = ['Draft', 'Finalized', 'Sent', 'Paid']
const stepIndex = computed(() => lifecycle.indexOf(status.value.name))

const customerName = computed(() => {
  if (!invoice.value) return ''
  return customersStore.getCustomerById(invoice.value.customerId)?.name ?? 'Unknown customer'
})

// PDF downloads have their own buttons in the header
const menuItems = computed<ActionMenuItem[]>(() => {
  const items: ActionMenuItem[] = []
  for (const action of actions.value.more) {
    if (action === 'separator') {
      if (items.length && !items[items.length - 1].separator) items.push({ separator: true })
    } else if (action.id !== 'downloadPdf' && action.id !== 'downloadReport') {
      items.push({ label: action.label, icon: action.icon, danger: action.danger, run: () => runAndFollow(action.id) })
    }
  }
  return items[0]?.separator ? items.slice(1) : items
})

function money(value: MoneyDto | undefined): string {
  return value ? formatMoney(value.amount, value.currency) : '—'
}

async function runAndFollow(action: InvoiceActionId) {
  if (!invoice.value) return
  const done = await run(action, invoice.value)
  if (done && (action === 'delete' || action === 'forceDelete')) router.push('/invoices')
}

async function loadData() {
  try {
    loading.value = true
    error.value = null
    await Promise.all([
      invoicesStore.fetchById(invoiceId.value),
      customersStore.customers.length ? Promise.resolve() : customersStore.fetchAll()
    ])
  } catch (err: any) {
    error.value = err.message || 'Failed to load invoice'
  } finally {
    loading.value = false
  }
}

onMounted(loadData)
watch(invoiceId, loadData)
watch(() => invoice.value?.invoiceNumber, number => setPageTitle(number ? `Invoice ${number}` : null), { immediate: true })
</script>

<style scoped>
.meta {
  display: inline-flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.25rem 0.55rem;
}

.dot {
  color: var(--color-text-subtle);
}

.lifecycle {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  margin: 0 0 1.25rem;
  padding: 0;
  list-style: none;
  counter-reset: none;
}

.lifecycle li {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.3rem 0.85rem 0.3rem 0.35rem;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-surface);
  color: var(--color-text-muted);
  font-size: var(--text-sm);
  font-weight: 500;
}

.marker {
  width: 1.3rem;
  height: 1.3rem;
  display: grid;
  place-items: center;
  border-radius: 50%;
  background: var(--color-surface-sunken);
  font-size: var(--text-xs);
  font-weight: 700;
}

.marker .app-icon {
  width: 0.8rem;
  height: 0.8rem;
  stroke-width: 3;
}

.lifecycle li.done {
  color: var(--color-text-secondary);
}

.lifecycle li.done .marker {
  background: var(--color-success);
  color: var(--color-on-status);
}

.lifecycle li.current {
  border-color: var(--color-primary-line);
  background: var(--color-primary-soft);
  color: var(--color-primary);
  font-weight: 600;
}

.lifecycle li.current .marker {
  background: var(--color-primary);
  color: var(--color-on-primary);
}

.cancelled-note {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1.25rem;
  padding: 0.6rem 0.85rem;
  border: 1px solid var(--color-danger-line);
  border-radius: var(--radius-md);
  background: var(--color-danger-soft);
  color: var(--color-danger);
  font-size: var(--text-md);
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1.6fr) minmax(18rem, 1fr);
  gap: 1rem;
  align-items: start;
}

.document {
  height: calc(100vh - 16rem);
  min-height: 30rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  overflow: hidden;
}

.side {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

@media (max-width: 1000px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
  }

  .document {
    height: 70vh;
  }
}

.amounts,
.facts {
  display: grid;
  gap: 0.6rem;
  margin: 0;
}

.amounts div,
.facts div {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
}

.amounts dt,
.facts dt {
  color: var(--color-text-muted);
  font-size: var(--text-md);
}

.amounts dd,
.facts dd {
  margin: 0;
  text-align: right;
  font-weight: 500;
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
  overflow-wrap: anywhere;
}

.amounts .total {
  padding-top: 0.6rem;
  border-top: 1px solid var(--color-border);
}

.amounts .total dt,
.amounts .total dd {
  font-size: var(--text-base);
  font-weight: 650;
  color: var(--color-text);
}

.mono {
  font-family: var(--font-mono);
  font-size: var(--text-md);
}

.notes {
  margin: 0;
  white-space: pre-line;
  color: var(--color-text-secondary);
}
</style>
