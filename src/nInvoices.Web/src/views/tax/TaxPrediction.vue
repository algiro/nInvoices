<template>
  <div class="tax-page">
    <PageHeader :title="t('nav.taxPrediction')" :subtitle="t('tax.subtitle')">
      <template #actions>
        <BaseButton icon="receipt" to="/expenses">{{ t('nav.expenses') }}</BaseButton>
        <select v-model.number="year" class="control year" :aria-label="t('common.year')">
          <option v-for="option in years" :key="option" :value="option">{{ option }}</option>
        </select>
      </template>
    </PageHeader>

    <LoadingState v-if="loading && !prediction" :label="t('tax.loading')" />

    <EmptyState v-else-if="loadError" icon="alert" :title="t('tax.loadFailed')" :description="loadError">
      <BaseButton @click="load">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="prediction && !prediction.isSetUp"
      icon="landmark"
      :title="t('tax.setUpTitle')"
      :description="prediction.warnings[0] ?? t('tax.setUpDescription')"
    >
      <BaseButton variant="primary" icon="settings" to="/settings/country">{{ t('invoiceEmail.openSettings') }}</BaseButton>
    </EmptyState>

    <template v-else-if="prediction">
      <div class="stat-tiles">
        <div class="stat-tile hero">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="landmark" /></span><span class="stat-tile-label">{{ t('tax.estimatedFor', { year }) }}</span></span>
          <span class="stat-tile-value">{{ formatMoney(totals.estimated, currency) }}</span>
          <span class="stat-tile-note">{{ t('tax.allPeriods') }}</span>
        </div>
        <div class="stat-tile" :style="{ '--accent': 'var(--kpi-2)' }">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="paid" /></span><span class="stat-tile-label">{{ t('tax.paidSoFar') }}</span></span>
          <span class="stat-tile-value">{{ formatMoney(totals.paid, currency) }}</span>
          <span class="stat-tile-note">{{ t('tax.recordedPayments') }}</span>
        </div>
        <div class="stat-tile" :style="{ '--accent': 'var(--kpi-3)' }">
          <span class="stat-tile-head"><span class="stat-tile-icon"><AppIcon name="calendar" /></span><span class="stat-tile-label">{{ t('tax.nextToPay') }}</span></span>
          <template v-if="due">
            <span class="stat-tile-value">{{ formatMoney(due.form.estimated, currency) }}</span>
            <span class="stat-tile-note">{{ t('tax.dueNote', { form: due.form.name, period: due.period.name, date: formatDate(due.form.deadline) }) }}</span>
          </template>
          <template v-else>
            <span class="stat-tile-value">—</span>
            <span class="stat-tile-note">{{ t('tax.nothingPending') }}</span>
          </template>
        </div>
      </div>

      <div v-if="prediction.warnings.length" class="warnings" role="status">
        <AppIcon name="alert" />
        <ul>
          <li v-for="warning in prediction.warnings" :key="warning">{{ warning }}</li>
        </ul>
      </div>

      <BasePanel
        v-for="period in prediction.periods"
        :key="period.period"
        flush
        :title="`${period.name} ${year}`"
        :description="`${formatDate(period.start, { day: 'numeric', month: 'short' })} – ${formatDate(period.end)}`"
      >
        <TaxFormRow
          v-for="form in period.forms"
          :key="form.code"
          :form="form"
          :period="period"
          :currency="currency"
          :today="today"
          @record="openPayment(period, $event)"
        />
      </BasePanel>

      <p class="disclaimer">
        {{ t('tax.disclaimer') }}
      </p>
    </template>

    <BaseDialog :open="payment !== null" :title="payment ? `${payment.form.name} · ${payment.period.name} ${year}` : ''" size="sm" :close-on-overlay="false" @close="payment = null">
      <RecordPaymentForm
        v-if="payment"
        :form="payment.form"
        :period="payment.period"
        :year="year"
        :currency="currency"
        @saved="onPaymentChanged(t('tax.paymentRecorded'))"
        @deleted="onPaymentChanged(t('tax.paymentDeleted'))"
        @cancel="payment = null"
      />
    </BaseDialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { taxPredictionApi } from '@/api'
import type { TaxFormDto, TaxPeriodDto, TaxPredictionDto } from '@/types'
import { useToast, errorMessage } from '@/composables/useToast'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import TaxFormRow from '@/components/tax/TaxFormRow.vue'
import RecordPaymentForm from '@/components/tax/RecordPaymentForm.vue'
import { formatDate, formatMoney } from '@/utils/format'
import { nextDue, todayIso, yearTotals } from '@/utils/taxPrediction'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const toast = useToast()

const today = todayIso()
const currentYear = Number(today.slice(0, 4))
const year = ref(currentYear)
const years = Array.from({ length: 5 }, (_, i) => currentYear + 1 - i)

const prediction = ref<TaxPredictionDto | null>(null)
const loading = ref(false)
const loadError = ref<string | null>(null)
const payment = ref<{ period: TaxPeriodDto; form: TaxFormDto } | null>(null)

const currency = computed(() => prediction.value?.currency ?? 'EUR')
const totals = computed(() => (prediction.value ? yearTotals(prediction.value) : { estimated: 0, paid: 0 }))
const due = computed(() => (prediction.value ? nextDue(prediction.value, today) : null))

async function load() {
  loading.value = true
  loadError.value = null
  try {
    prediction.value = await taxPredictionApi.get(year.value)
  } catch (err) {
    loadError.value = errorMessage(err, t('tax.loadFailed'))
  } finally {
    loading.value = false
  }
}

function openPayment(period: TaxPeriodDto, form: TaxFormDto) {
  payment.value = { period, form }
}

async function onPaymentChanged(message: string) {
  payment.value = null
  toast.success(message)
  // Later periods of a cumulative form start from what was paid
  await load()
}

watch(year, load)
onMounted(load)
</script>

<style scoped>
.tax-page {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 72rem;
}

.year {
  width: auto;
}

.stat-tiles {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
  gap: 0.75rem;
}

.warnings {
  display: flex;
  gap: 0.6rem;
  padding: 0.75rem 0.9rem;
  border: 1px solid var(--color-border);
  border-left: 3px solid var(--color-warning, var(--color-primary));
  border-radius: var(--radius-md);
  background: var(--color-surface);
  font-size: var(--text-md);
}

.warnings ul {
  margin: 0;
  padding: 0;
  list-style: none;
}

.disclaimer {
  margin: 0;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}
</style>
