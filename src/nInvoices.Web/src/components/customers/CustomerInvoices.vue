<template>
  <section class="customer-invoices">
    <header class="section-header">
      <div>
        <h3>{{ t('nav.invoices') }}</h3>
        <p class="help">{{ t('customers.invoicesHelp') }}</p>
      </div>
      <BaseButton variant="primary" icon="plus" :to="{ path: '/invoices/new', query: { customerId } }">{{ t('layout.newInvoice') }}</BaseButton>
    </header>

    <LoadingState v-if="loading" :label="t('invoices.loading')" />

    <EmptyState v-else-if="loadError" icon="alert" :title="t('invoices.loadFailed')" :description="loadError" compact>
      <BaseButton @click="load">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="invoices.length === 0"
      icon="invoices"
      :title="t('invoices.emptyTitle')"
      :description="t('customers.invoicesEmpty')"
      compact
    />

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">{{ t('invoices.columns.number') }}</th>
            <th scope="col">{{ t('invoices.columns.period') }}</th>
            <th scope="col">{{ t('invoices.columns.issued') }}</th>
            <th scope="col">{{ t('invoices.columns.status') }}</th>
            <th scope="col" class="num">{{ t('invoices.columns.total') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="invoice in invoices" :key="invoice.id" class="clickable" @click="router.push(`/invoices/${invoice.id}`)">
            <td>
              <router-link :to="`/invoices/${invoice.id}`" class="primary-cell number" @click.stop>
                {{ invoice.invoiceNumber }}
              </router-link>
              <span class="sub">{{ invoiceTypeLabel(invoice.type) }}</span>
            </td>
            <td>{{ invoice.month ? formatPeriod(invoice.month, invoice.year) : '—' }}</td>
            <td>{{ formatDate(invoice.issueDate) }}</td>
            <td><StatusPill :tone="invoiceStatus(invoice.status).tone">{{ invoiceStatus(invoice.status).label }}</StatusPill></td>
            <td class="num primary-cell">{{ formatMoney(invoice.total.amount, invoice.total.currency) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { invoicesApi } from '@/api/invoices'
import type { InvoiceDto } from '@/types'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { formatMoney, formatDate, formatPeriod, invoiceStatus, invoiceTypeLabel } from '@/utils/format'
import { errorMessage } from '@/composables/useToast'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps<{ customerId: number }>()

const router = useRouter()
const invoices = ref<InvoiceDto[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const list = await invoicesApi.getByCustomer(props.customerId)
    invoices.value = [...list].sort((a, b) => b.issueDate.localeCompare(a.issueDate) || b.id - a.id)
  } catch (error) {
    loadError.value = errorMessage(error)
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(() => props.customerId, load)
</script>

<style scoped>
.customer-invoices {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.section-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
}

.section-header h3 {
  font-size: 1rem;
}

.help {
  margin: 0.2rem 0 0;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.number {
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}

.number:hover {
  color: var(--color-primary);
}
</style>
