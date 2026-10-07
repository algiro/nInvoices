<template>
  <div class="tiles">
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
</template>

<script setup lang="ts">
import type { InvoiceSummaryDto, MoneyDto } from '@/types'
import AppIcon from '@/components/ui/AppIcon.vue'
import { useInvoiceListQuery } from '@/composables/useInvoiceListQuery'
import { formatMoney } from '@/utils/format'

defineProps<{ summary: InvoiceSummaryDto }>()

const { applyView } = useInvoiceListQuery()
const currentYear = new Date().getFullYear()

// Money totals are kept per currency; adding EUR to USD would be meaningless
function formatTotals(totals: MoneyDto[]): string {
  if (totals.length === 0) return formatMoney(0, 'EUR')
  return totals.map(t => formatMoney(t.amount, t.currency)).join(' + ')
}
</script>

<style scoped>
.tiles {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
  gap: 0.75rem;
  margin-bottom: 1.25rem;
}
</style>
