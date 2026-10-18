<template>
  <BasePanel :title="t('invoiceWizard.stepCustomer')" :description="t('invoiceWizard.customerStep.description')">
    <div class="grid">
      <BaseField :label="t('customers.customer')" for="customer" required class="span-2">
        <select id="customer" v-model="form.customerId" class="control">
          <option :value="0" disabled>{{ t('invoiceWizard.customerStep.selectCustomer') }}</option>
          <option v-for="c in sortedCustomers" :key="c.id" :value="c.id">
            {{ c.name }}
          </option>
        </select>
      </BaseField>

      <BaseField :label="t('invoiceWizard.customerStep.invoiceType')" for="type" required>
        <select id="type" v-model="form.invoiceType" class="control">
          <option :value="InvoiceType.Monthly">{{ t('invoiceType.Monthly') }}</option>
          <option :value="InvoiceType.OneTime">{{ t('invoiceType.OneTime') }}</option>
        </select>
      </BaseField>

      <BaseField :label="t('invoiceWizard.issueDate')" for="issueDate" required>
        <input id="issueDate" v-model="form.issueDate" type="date" class="control" />
      </BaseField>

      <BaseField v-if="isMonthly" :label="t('invoiceWizard.customerStep.billingPeriod')" for="period-month" required class="span-2">
        <div class="period">
          <BaseButton icon="arrowLeft" icon-only variant="secondary" :aria-label="t('common.previousMonth')" :title="t('common.previousMonth')" @click="shiftMonth(-1)" />
          <select id="period-month" v-model="selectedMonth" class="control" :aria-label="t('common.month')">
            <option v-for="month in months" :key="month.value" :value="month.value">{{ month.label }}</option>
          </select>
          <select id="period-year" v-model="selectedYear" class="control year" :aria-label="t('common.year')">
            <option v-for="year in years" :key="year" :value="year">{{ year }}</option>
          </select>
          <BaseButton icon="chevronRight" icon-only variant="secondary" :aria-label="t('common.nextMonth')" :title="t('common.nextMonth')" @click="shiftMonth(1)" />
        </div>
      </BaseField>

      <BaseField
        v-if="isMonthly && availableTemplates.length > 0"
        :label="t('invoiceWizard.customerStep.timesheetTemplate')"
        for="monthlyReportTemplate"
        class="span-2"
        :help="t('invoiceWizard.customerStep.timesheetTemplateHelp')"
      >
        <select id="monthlyReportTemplate" v-model="form.monthlyReportTemplateId" class="control">
          <option :value="undefined">{{ activeTemplateName ? t('invoiceWizard.customerStep.activeTemplateNamed', { name: activeTemplateName }) : t('invoiceWizard.customerStep.activeTemplate') }}</option>
          <option v-for="template in availableTemplates" :key="template.id" :value="template.id">
            {{ template.name }}{{ template.customerId === null ? ` (${t('templates.sharedSuffix')})` : '' }}{{ template.isActive ? ` (${t('templates.activeSuffix')})` : '' }}
          </option>
        </select>
      </BaseField>
    </div>

    <div v-if="form.customerId && customerRates.length > 0" class="grid rate-fields">
      <BaseField
        v-if="customerRates.length > 1 && !isMonthly"
        :label="t('rates.rate')"
        for="rate"
        required
        :class="{ 'span-2': !showHours }"
        :help="t('invoiceWizard.customerStep.rateHelp')"
      >
        <select id="rate" v-model="form.rateId" class="control">
          <option v-for="rate in customerRates" :key="rate.id" :value="rate.id">
            {{ rateLabel(rate) }}
          </option>
        </select>
      </BaseField>

      <BaseField
        v-if="showHours"
        :label="t('invoiceWizard.hours')"
        for="hours"
        required
        :class="{ 'span-2': customerRates.length <= 1 }"
        :help="hoursHelp"
      >
        <input id="hours" v-model.number="form.hours" type="number" min="0" step="0.25" class="control num" placeholder="0" />
      </BaseField>
    </div>

    <div v-if="form.customerId" class="rate-line" :class="{ missing: noRate }">
      <template v-if="isMonthly && customerRates.length > 1">
        <AppIcon name="coins" />
        <span>{{ t('invoiceWizard.customerStep.severalRates', { n: customerRates.length }) }}</span>
      </template>
      <template v-else-if="selectedRate">
        <AppIcon name="coins" />
        <i18n-t keypath="invoiceWizard.customerStep.billedAt" tag="span"><template #price><strong>{{ formatMoney(selectedRate.price.amount, selectedRate.price.currency) }}</strong></template><template #unit>{{ rateUnit }}</template></i18n-t>
      </template>
      <template v-else-if="noRate">
        <AppIcon name="alert" />
        <span>{{ t('invoiceWizard.customerStep.noRate') }}</span>
        <router-link :to="{ path: `/customers/${form.customerId}`, query: { tab: 'rates' } }">{{ t('invoiceWizard.customerStep.addRate') }}</router-link>
      </template>
    </div>
  </BasePanel>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { InvoiceType, RateType } from '@/types'
import type { RateDto } from '@/types'
import type { InvoiceDraft } from '@/composables/useInvoiceDraft'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import { formatMoney } from '@/utils/format'
import { currentLanguage } from '@/i18n'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps<{ draft: InvoiceDraft }>()

// Top-level refs are unwrapped in the template
const {
  form,
  selectedMonth,
  selectedYear,
  selectedRate,
  customerRates,
  noRate,
  isMonthly,
  isHourlyRate,
  years,
  sortedCustomers,
  availableTemplates,
  activeTemplateName,
  rateUnit,
  shiftMonth
} = props.draft

// A one-time invoice with an hourly rate bills the hours entered here
const showHours = computed(() => !isMonthly.value && isHourlyRate.value)
const hoursHelp = computed(() => {
  const rate = selectedRate.value
  const hours = Number(form.hours)
  return rate && hours > 0 ? `${hours}h × ${formatMoney(rate.price.amount, rate.price.currency)} = ${formatMoney(hours * rate.price.amount, rate.price.currency)}` : t('invoiceWizard.customerStep.hoursHelp')
})

function rateLabel(rate: RateDto): string {
  const unit = rate.type === RateType.Hourly ? t('rates.perHour') : rate.type === RateType.Monthly ? t('rates.perMonth') : t('rates.perDay')
  return `${formatMoney(rate.price.amount, rate.price.currency)} ${unit}`
}

const months = computed(() => Array.from({ length: 12 }, (_, i) => ({
  value: i + 1,
  label: new Date(2000, i, 1).toLocaleDateString(currentLanguage(), { month: 'long' })
})))
</script>

<style scoped>
.grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1rem 1.1rem;
}

.span-2 {
  grid-column: span 2;
}

.rate-fields {
  margin-top: 1rem;
}

@media (max-width: 900px) {
  .grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 560px) {
  .grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .span-2 {
    grid-column: auto;
  }
}

.period {
  display: flex;
  gap: 0.4rem;
}

.period .control {
  flex: 1;
}

.period .year {
  flex: 0 0 6.5rem;
}

.rate-line {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.45rem;
  margin-top: 1rem;
  padding: 0.55rem 0.75rem;
  border-radius: var(--radius-md);
  background: var(--color-surface-muted);
  font-size: var(--text-md);
  color: var(--color-text-secondary);
}

.rate-line .app-icon {
  color: var(--color-primary);
}

.rate-line.missing {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}

.rate-line.missing .app-icon {
  color: var(--color-warning);
}

.rate-line strong {
  color: var(--color-text);
}
</style>
