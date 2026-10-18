<template>
  <div class="invoice-details">
    <LoadingState v-if="loading && !invoice" :label="t('invoices.details.loading')" />

    <EmptyState v-else-if="error && !invoice" icon="alert" :title="t('invoices.details.loadFailed')" :description="error">
      <BaseButton @click="loadData">{{ t('common.tryAgain') }}</BaseButton>
      <BaseButton variant="ghost" to="/invoices">{{ t('invoices.details.backToList') }}</BaseButton>
    </EmptyState>

    <template v-else-if="invoice">
      <PageHeader :title="t('invoices.details.title', { number: invoice.invoiceNumber })">
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
            {{ t('invoices.details.pdf') }}
          </BaseButton>
          <BaseButton
            v-if="isMonthly(invoice)"
            icon="calendar"
            :loading="isBusy(invoice, 'downloadReport')"
            @click="run('downloadReport', invoice)"
          >
            {{ t('invoices.details.monthlyReport') }}
          </BaseButton>
          <BaseButton v-if="canEmail" icon="send" @click="emailOpen = true">{{ t('invoices.details.email') }}</BaseButton>
          <BaseButton
            v-if="actions.primary"
            variant="primary"
            :icon="actions.primary.icon"
            :loading="isBusy(invoice, actions.primary.id)"
            @click="runAndFollow(actions.primary.id)"
          >
            {{ actions.primary.label }}
          </BaseButton>
          <ActionMenu :items="menuItems" :label="t('invoices.details.moreActions')" />
        </template>
      </PageHeader>

      <ol v-if="status.name !== 'Cancelled'" class="lifecycle" :aria-label="t('invoices.details.progress')">
        <li
          v-for="(step, index) in lifecycle"
          :key="step"
          :class="{ done: index < stepIndex, current: index === stepIndex }"
          :aria-current="index === stepIndex ? 'step' : undefined"
        >
          <span class="marker"><AppIcon v-if="index < stepIndex" name="check" /><template v-else>{{ index + 1 }}</template></span>
          {{ invoiceStatus(step).label }}
        </li>
      </ol>
      <div v-else class="cancelled-note" role="status">
        <AppIcon name="ban" />
        {{ t('invoices.details.cancelledNote') }}
      </div>

      <div class="layout">
        <TemplatePreviewPane
          class="document"
          :title="t('invoices.details.document')"
          :html="invoice.renderedContent ?? null"
          :loading="false"
          :stale="false"
          :caption="t('invoices.details.documentCaption')"
        />

        <div class="side">
          <BasePanel :title="t('invoices.details.amounts')">
            <dl class="amounts">
              <div><dt>{{ t('invoiceWizard.review.subtotal') }}</dt><dd>{{ money(invoice.subtotal) }}</dd></div>
              <div v-if="invoice.totalExpenses?.amount"><dt>{{ t('nav.expenses') }}</dt><dd>{{ money(invoice.totalExpenses) }}</dd></div>
              <div><dt>{{ t('customers.tabs.taxes') }}</dt><dd>{{ money(invoice.totalTaxes) }}</dd></div>
              <div class="total"><dt>{{ t('invoices.columns.total') }}</dt><dd>{{ money(invoice.total) }}</dd></div>
            </dl>
          </BasePanel>

          <BasePanel :title="t('customers.details')">
            <dl class="facts">
              <div><dt>{{ t('customers.customer') }}</dt><dd><router-link :to="`/customers/${invoice.customerId}`">{{ customerName }}</router-link></dd></div>
              <div><dt>{{ t('invoiceWizard.review.invoiceNumber') }}</dt><dd class="mono">{{ invoice.invoiceNumber }}</dd></div>
              <div><dt>{{ t('invoiceWizard.issueDate') }}</dt><dd>{{ formatDate(invoice.issueDate) }}</dd></div>
              <div><dt>{{ t('invoices.details.dueDate') }}</dt><dd>{{ invoice.dueDate ? formatDate(invoice.dueDate) : t('common.notSet') }}</dd></div>
              <div v-if="invoice.month"><dt>{{ t('invoices.columns.period') }}</dt><dd>{{ formatPeriod(invoice.month, invoice.year) }}</dd></div>
              <div v-if="invoice.workedDays !== undefined && invoice.workedDays !== null"><dt>{{ t('invoiceWizard.workedDays') }}</dt><dd>{{ invoice.workedDays }}</dd></div>
              <div><dt>{{ t('invoices.details.created') }}</dt><dd>{{ formatDate(invoice.createdAt) }}</dd></div>
              <div v-if="invoice.updatedAt && invoice.updatedAt !== invoice.createdAt"><dt>{{ t('invoices.details.lastChanged') }}</dt><dd>{{ formatDate(invoice.updatedAt) }}</dd></div>
            </dl>
          </BasePanel>

          <!-- What country rules show for the invoice (countries/<code>), e.g. Spain's Verifactu record and QR code -->
          <component :is="panel" v-for="(panel, index) in countryInvoicePanels" :key="index" :invoice-id="invoice.id" :status="status.name" />

          <InvoiceEInvoicePanel :invoice-id="invoice.id" :status="status.name" />

          <BasePanel v-if="canEmail || emails.length" :title="t('invoices.details.emails')" :description="t('invoices.details.emailsDescription')">
            <ul v-if="emails.length" class="emails">
              <li v-for="email in emails" :key="email.id">
                <div class="email-line">
                  <span class="email-subject">{{ email.subject }}</span>
                  <a :href="email.gmailUrl" target="_blank" rel="noopener" class="email-open">{{ t('invoices.details.openInGmail') }}</a>
                </div>
                <span class="muted">{{ t('invoices.details.emailTo', { to: email.to, date: formatDate(email.createdAt, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) }) }}</span>
              </li>
            </ul>
            <i18n-t v-else keypath="invoices.details.noEmails" tag="p" class="muted">
              <template #create><button type="button" class="link-button" @click="emailOpen = true">{{ t('invoices.details.createEmail') }}</button></template>
            </i18n-t>
          </BasePanel>

          <BasePanel v-if="invoice.notes" :title="t('invoices.details.notes')">
            <p class="notes">{{ invoice.notes }}</p>
          </BasePanel>
        </div>
      </div>

      <InvoiceEmailDialog
        :open="emailOpen"
        :invoice="invoice"
        @close="emailOpen = false"
        @created="email => emails.unshift(email)"
        @mark-sent="markSentAfterEmail"
      />
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useInvoicesStore } from '@/stores/invoices'
import { useCustomersStore } from '@/stores/customers'
import type { MoneyDto, InvoiceEmailDto } from '@/types'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import ActionMenu, { type ActionMenuItem } from '@/components/ui/ActionMenu.vue'
import TemplatePreviewPane from '@/components/templates/editor/TemplatePreviewPane.vue'
import InvoiceEmailDialog from '@/components/invoices/InvoiceEmailDialog.vue'
import InvoiceEInvoicePanel from '@/components/invoices/InvoiceEInvoicePanel.vue'
import { countryInvoicePanels } from '@/countries'
import { invoicesApi } from '@/api/invoices'
import { useInvoiceActions, actionsFor, isMonthly, type InvoiceActionId } from '@/composables/useInvoiceActions'
import { setPageTitle } from '@/composables/usePageTitle'
import { formatMoney, formatDate, formatPeriod, invoiceStatus, invoiceTypeLabel } from '@/utils/format'
import { errorMessage } from '@/composables/useToast'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

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

// Only a finalized invoice has its final PDF; a cancelled one must not go out
const canEmail = computed(() => ['Finalized', 'Sent', 'Paid'].includes(status.value.name))
const emailOpen = ref(false)
const emails = ref<InvoiceEmailDto[]>([])

async function loadEmails() {
  try {
    emails.value = await invoicesApi.getEmails(invoiceId.value)
  } catch {
    emails.value = [] // the history is secondary; the rest of the page still works
  }
}

async function markSentAfterEmail() {
  emailOpen.value = false
  await runAndFollow('markSent')
}

const lifecycle = ['Draft', 'Finalized', 'Sent', 'Paid']
const stepIndex = computed(() => lifecycle.indexOf(status.value.name))

const customerName = computed(() => {
  if (!invoice.value) return ''
  return customersStore.getCustomerById(invoice.value.customerId)?.name ?? t('invoices.details.unknownCustomer')
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
  } catch (err) {
    error.value = errorMessage(err, t('invoices.details.loadFailed'))
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadData()
  loadEmails()
})
watch(invoiceId, () => {
  loadData()
  loadEmails()
})
watch(() => invoice.value?.invoiceNumber, number => setPageTitle(number ? t('invoices.details.title', { number }) : null), { immediate: true })
</script>

<style scoped>
.emails {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  margin: 0;
  padding: 0;
  list-style: none;
}

.emails li {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  min-width: 0;
}

.email-line {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.6rem;
}

.email-subject {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 600;
}

.email-open {
  flex: none;
  font-size: var(--text-sm);
}

.muted {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.link-button {
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font: inherit;
  cursor: pointer;
  text-decoration: underline;
}

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
