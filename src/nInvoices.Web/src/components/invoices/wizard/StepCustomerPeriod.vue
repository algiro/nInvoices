<template>
  <BasePanel title="Customer &amp; period" description="Who the invoice is for and which month it bills.">
    <div class="grid">
      <BaseField label="Customer" for="customer" required class="span-2">
        <select id="customer" v-model="form.customerId" class="control">
          <option :value="0" disabled>Select a customer</option>
          <option v-for="c in sortedCustomers" :key="c.id" :value="c.id">
            {{ c.name }}
          </option>
        </select>
      </BaseField>

      <BaseField label="Invoice type" for="type" required>
        <select id="type" v-model="form.invoiceType" class="control">
          <option :value="InvoiceType.Monthly">Monthly</option>
          <option :value="InvoiceType.OneTime">One-time</option>
        </select>
      </BaseField>

      <BaseField label="Issue date" for="issueDate" required>
        <input id="issueDate" v-model="form.issueDate" type="date" class="control" />
      </BaseField>

      <BaseField v-if="isMonthly" label="Billing period" for="period-month" required class="span-2">
        <div class="period">
          <BaseButton icon="arrowLeft" icon-only variant="secondary" aria-label="Previous month" title="Previous month" @click="shiftMonth(-1)" />
          <select id="period-month" v-model="selectedMonth" class="control" aria-label="Month">
            <option v-for="month in months" :key="month.value" :value="month.value">{{ month.label }}</option>
          </select>
          <select id="period-year" v-model="selectedYear" class="control year" aria-label="Year">
            <option v-for="year in years" :key="year" :value="year">{{ year }}</option>
          </select>
          <BaseButton icon="chevronRight" icon-only variant="secondary" aria-label="Next month" title="Next month" @click="shiftMonth(1)" />
        </div>
      </BaseField>

      <BaseField
        v-if="isMonthly && availableTemplates.length > 0"
        label="Timesheet template"
        for="monthlyReportTemplate"
        class="span-2"
        help="Layout of the monthly report PDF."
      >
        <select id="monthlyReportTemplate" v-model="form.monthlyReportTemplateId" class="control">
          <option :value="undefined">Active template{{ activeTemplateName ? ` (${activeTemplateName})` : '' }}</option>
          <option v-for="template in availableTemplates" :key="template.id" :value="template.id">
            {{ template.name }}{{ template.isActive ? ' (active)' : '' }}
          </option>
        </select>
      </BaseField>
    </div>

    <div v-if="form.customerId" class="rate-line" :class="{ missing: noRate }">
      <template v-if="selectedRate">
        <AppIcon name="coins" />
        <span>Billed at <strong>{{ formatMoney(selectedRate.price.amount, selectedRate.price.currency) }}</strong> {{ rateUnit }}</span>
      </template>
      <template v-else-if="noRate">
        <AppIcon name="alert" />
        <span>This customer has no rate yet, so nothing can be billed.</span>
        <router-link :to="{ path: `/customers/${form.customerId}`, query: { tab: 'rates' } }">Add a rate</router-link>
      </template>
    </div>
  </BasePanel>
</template>

<script setup lang="ts">
import { InvoiceType } from '@/types'
import type { InvoiceDraft } from '@/composables/useInvoiceDraft'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import { formatMoney } from '@/utils/format'

const props = defineProps<{ draft: InvoiceDraft }>()

// Top-level refs are unwrapped in the template
const {
  form,
  selectedMonth,
  selectedYear,
  selectedRate,
  noRate,
  isMonthly,
  years,
  sortedCustomers,
  availableTemplates,
  activeTemplateName,
  rateUnit,
  shiftMonth
} = props.draft

const months = Array.from({ length: 12 }, (_, i) => ({
  value: i + 1,
  label: new Date(2000, i, 1).toLocaleDateString('en-US', { month: 'long' })
}))
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
