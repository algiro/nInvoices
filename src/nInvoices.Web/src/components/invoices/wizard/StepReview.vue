<template>
  <div class="review">
    <BasePanel :title="t('invoiceWizard.review.summary')" class="summary-panel">
      <dl class="facts">
        <div><dt>{{ t('invoiceWizard.review.invoiceNumber') }}</dt><dd>{{ preview?.invoiceNumber ?? '…' }}</dd></div>
        <div><dt>{{ t('customers.customer') }}</dt><dd>{{ customer?.name ?? '-' }}</dd></div>
        <div><dt>{{ t('invoiceWizard.issueDate') }}</dt><dd>{{ issueDateLabel }}</dd></div>
        <div v-if="isMonthly"><dt>{{ t('invoices.columns.period') }}</dt><dd>{{ periodLabel }}</dd></div>
        <template v-if="isMonthly">
          <div><dt>{{ t('invoiceWizard.workedDays') }}</dt><dd>{{ totals.workedDays }}</dd></div>
          <div v-if="isDailyRate"><dt>{{ t('invoiceWizard.review.billableDays') }}</dt><dd>{{ totals.effectiveDays.toFixed(2) }}</dd></div>
          <div><dt>{{ t('invoiceWizard.hours') }}</dt><dd>{{ totals.totalHours.toFixed(1) }}</dd></div>
          <div v-if="totals.publicHolidays || totals.unpaidLeave"><dt>{{ t('invoiceWizard.review.holidayLeave') }}</dt><dd>{{ totals.publicHolidays }} · {{ totals.unpaidLeave }}</dd></div>
        </template>
      </dl>

      <dl v-if="preview?.total" class="amounts">
        <div><dt>{{ t('invoiceWizard.review.subtotal') }}</dt><dd>{{ money(preview.subtotal) }}</dd></div>
        <div v-if="preview.totalExpenses?.amount"><dt>{{ t('nav.expenses') }}</dt><dd>{{ money(preview.totalExpenses) }}</dd></div>
        <div v-for="tax in preview.taxes" :key="tax.description">
          <dt>{{ tax.description }} ({{ tax.rate }}%)</dt><dd>{{ money(tax.amount) }}</dd>
        </div>
        <div class="total"><dt>{{ t('invoices.columns.total') }}</dt><dd>{{ money(preview.total) }}</dd></div>
      </dl>

      <p class="note muted">{{ t('invoiceWizard.review.note') }}</p>
    </BasePanel>

    <BasePanel flush class="preview-panel">
      <template #header>
        <BaseTabs v-model="tab" :tabs="tabs" :label="t('invoiceWizard.review.document')" />
      </template>
      <template #actions>
        <BaseButton size="sm" variant="ghost" icon="refresh" :loading="loading" @click="refresh">{{ t('common.refresh') }}</BaseButton>
      </template>

      <div v-if="preview && preview.errors.length && tab === 'invoice'" class="problem" role="alert">
        <AppIcon name="error" />
        <div>
          <strong>{{ t('invoiceWizard.review.cannotGenerate') }}</strong>
          <ul><li v-for="error in preview.errors" :key="error">{{ error }}</li></ul>
          <router-link :to="{ path: `/customers/${form.customerId}`, query: { tab: 'templates' } }">{{ t('invoiceWizard.review.checkTemplates') }}</router-link>
        </div>
      </div>
      <div v-else-if="preview?.timesheetError && tab === 'timesheet'" class="problem warning" role="status">
        <AppIcon name="alert" />
        <div>
          <strong>{{ t('invoiceWizard.review.timesheetCannotRender') }}</strong>
          <p>{{ preview.timesheetError }}</p>
          <p class="muted">{{ t('invoiceWizard.review.timesheetStillGenerate') }}</p>
        </div>
      </div>
      <div v-else class="preview-frame">
        <TemplatePreviewPane
          :title="tab === 'invoice' ? t('invoiceWizard.review.invoice') : t('invoiceWizard.review.timesheet')"
          :html="tab === 'invoice' ? preview?.invoiceHtml ?? null : preview?.timesheetHtml ?? null"
          :loading="loading"
          :stale="false"
          :caption="t('invoiceWizard.review.caption', { customer: customer?.name ?? '' })"
        />
      </div>
    </BasePanel>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import type { InvoiceDraft } from '@/composables/useInvoiceDraft'
import type { InvoiceDraftPreviewDto, MoneyDto } from '@/types'
import { invoicesApi } from '@/api/invoices'
import { useToast } from '@/composables/useToast'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseTabs, { type TabItem } from '@/components/ui/BaseTabs.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import TemplatePreviewPane from '@/components/templates/editor/TemplatePreviewPane.vue'
import { formatMoney } from '@/utils/format'
import { currentLanguage } from '@/i18n'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps<{ draft: InvoiceDraft }>()
const emit = defineEmits<{
  /** Why the invoice can't be generated according to the preview; null when it can. */
  (e: 'blocker', reason: string | null): void
}>()

const { form, customer, isMonthly, isDailyRate, periodLabel, totals, buildPayload } = props.draft
const toast = useToast()

const preview = ref<InvoiceDraftPreviewDto | null>(null)
const loading = ref(false)
const tab = ref('invoice')

const tabs = computed((): TabItem[] =>
  isMonthly.value
    ? [{ id: 'invoice', label: t('invoiceWizard.review.invoice') }, { id: 'timesheet', label: t('invoiceWizard.review.timesheet') }]
    : [{ id: 'invoice', label: t('invoiceWizard.review.invoice') }]
)

const issueDateLabel = computed(() =>
  form.issueDate ? new Date(`${form.issueDate}T00:00:00`).toLocaleDateString(currentLanguage(), { dateStyle: 'medium' }) : '-'
)

function money(value: MoneyDto | null) {
  return value ? formatMoney(value.amount, value.currency) : '-'
}

// Only the latest request may update the preview
let requestId = 0

async function refresh() {
  const id = ++requestId
  loading.value = true
  emit('blocker', t('invoiceWizard.review.rendering'))
  try {
    const result = await invoicesApi.previewDraft(buildPayload())
    if (id !== requestId) return
    preview.value = result
    emit('blocker', result.errors.length ? result.errors[0] : null)
  } catch (error) {
    if (id !== requestId) return
    preview.value = null
    emit('blocker', t('invoiceWizard.review.previewFailed'))
    toast.failure(t('invoiceWizard.review.previewFailedTitle'), error)
  } finally {
    if (id === requestId) loading.value = false
  }
}

// An invoice that can't be generated is the first thing to see
watch(preview, value => {
  if (value?.errors.length) tab.value = 'invoice'
})

onMounted(refresh)
</script>

<style scoped>
.review {
  display: grid;
  grid-template-columns: 20rem minmax(0, 1fr);
  gap: 1rem;
  align-items: start;
}

@media (max-width: 1000px) {
  .review {
    grid-template-columns: minmax(0, 1fr);
  }
}

.facts,
.amounts {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  margin: 0;
}

.facts div,
.amounts div {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  font-size: var(--text-md);
}

dt {
  color: var(--color-text-muted);
}

dd {
  margin: 0;
  font-weight: 600;
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
  text-align: right;
}

.amounts {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--color-border);
}

.amounts .total {
  margin-top: 0.25rem;
  padding-top: 0.6rem;
  border-top: 1px solid var(--color-border);
}

.amounts .total dt {
  color: var(--color-text);
  font-weight: 600;
}

.amounts .total dd {
  font-size: 1.15rem;
  color: var(--color-primary);
}

.note {
  margin: 1rem 0 0;
  font-size: var(--text-sm);
}

.preview-frame {
  height: min(78vh, 60rem);
  min-height: 24rem;
}

.problem {
  display: flex;
  gap: 0.6rem;
  margin: 1rem;
  padding: 0.8rem 1rem;
  border-radius: var(--radius-md);
  background: var(--color-danger-soft);
  color: var(--color-danger);
  font-size: var(--text-md);
}

.problem.warning {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}

.problem strong {
  color: var(--color-text);
}

.problem ul,
.problem p {
  margin: 0.3rem 0;
  padding-left: 0;
  list-style: none;
  color: var(--color-text-secondary);
}
</style>
