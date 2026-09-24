<template>
  <section class="customer-invoices">
    <header class="section-header">
      <div>
        <h3>Invoices</h3>
        <p class="help">Everything billed to this customer, newest first.</p>
      </div>
      <BaseButton variant="primary" icon="plus" :to="{ path: '/invoices/new', query: { customerId } }">New invoice</BaseButton>
    </header>

    <LoadingState v-if="loading" label="Loading invoices…" />

    <EmptyState v-else-if="loadError" icon="alert" title="Invoices could not be loaded" :description="loadError" compact>
      <BaseButton @click="load">Try again</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="invoices.length === 0"
      icon="invoices"
      title="No invoices yet"
      description="Generated invoices for this customer will be listed here."
      compact
    />

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">Number</th>
            <th scope="col">Period</th>
            <th scope="col">Issued</th>
            <th scope="col">Status</th>
            <th scope="col" class="num">Total</th>
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
  } catch (error: any) {
    loadError.value = error?.message ?? 'Unknown error'
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
