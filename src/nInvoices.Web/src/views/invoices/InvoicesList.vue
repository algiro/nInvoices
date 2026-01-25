<template>
  <div class="invoices-list">
    <div class="header">
      <h1 class="text-3xl font-bold">Invoices</h1>
      <button @click="handleGenerate" class="btn-primary">
        <svg class="w-5 h-5 inline mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        Generate Invoice
      </button>
    </div>

    <div class="filters">
      <input
        v-model="searchQuery"
        type="text"
        placeholder="Search invoices..."
        class="search-input"
      />
      
      <select v-model="statusFilter" class="filter-select">
        <option value="">All Statuses</option>
        <option value="Draft">Draft</option>
        <option value="Finalized">Finalized</option>
        <option value="Sent">Sent</option>
        <option value="Paid">Paid</option>
        <option value="Cancelled">Cancelled</option>
      </select>

      <select v-model="typeFilter" class="filter-select">
        <option value="">All Types</option>
        <option value="Monthly">Monthly</option>
        <option value="OneTime">One-Time</option>
      </select>
    </div>

    <div v-if="invoicesStore.loading" class="loading-state">
      <div class="spinner"></div>
      <p class="mt-4">Loading invoices...</p>
    </div>

    <div v-else-if="invoicesStore.error" class="error-state">
      <p class="text-red-600">{{ invoicesStore.error }}</p>
      <button @click="loadData" class="btn-primary mt-4">Retry</button>
    </div>

    <div v-else-if="filteredInvoices.length === 0" class="empty-state">
      <svg class="w-16 h-16 mx-auto text-gray-400 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
      </svg>
      <p class="text-xl text-gray-600 mb-2">
        {{ searchQuery || statusFilter || typeFilter ? 'No invoices found' : 'No invoices yet' }}
      </p>
      <p class="text-gray-500 mb-4">
        {{ searchQuery || statusFilter || typeFilter ? 'Try adjusting your filters' : 'Generate your first invoice to get started' }}
      </p>
      <button v-if="!searchQuery && !statusFilter && !typeFilter" @click="handleGenerate" class="btn-primary">
        Generate First Invoice
      </button>
    </div>

    <div v-else class="invoices-table-container">
      <table class="invoices-table">
        <thead>
          <tr>
            <th>Invoice #</th>
            <th>Customer</th>
            <th>Type</th>
            <th>Issue Date</th>
            <th>Due Date</th>
            <th>Amount</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="invoice in filteredInvoices"
            :key="invoice.id"
            @click="handleView(invoice.id)"
            class="invoice-row"
          >
            <td class="font-semibold">{{ invoice.invoiceNumber }}</td>
            <td>{{ getCustomerName(invoice.customerId) }}</td>
            <td>
              <span class="type-badge" :class="`type-${getTypeCssClass(invoice.type)}`">
                {{ formatType(invoice.type) }}
              </span>
            </td>
            <td>{{ formatDate(invoice.issueDate) }}</td>
            <td>{{ invoice.dueDate ? formatDate(invoice.dueDate) : '-' }}</td>
            <td class="font-semibold">{{ formatMoney(invoice.total) }}</td>
            <td>
              <span class="status-badge" :class="`status-${getStatusCssClass(invoice.status)}`">
                {{ formatStatus(invoice.status) }}
              </span>
            </td>
            <td class="actions-cell" @click.stop>
              <button
                @click.prevent.stop="handleDownloadPdf(invoice.id)"
                class="action-btn"
                title="Download PDF"
              >
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                </svg>
              </button>
              <!-- Normal delete button for drafts -->
              <button
                v-if="invoice.status === 'Draft' || invoice.status === 0"
                @click.prevent.stop="handleDelete(invoice)"
                class="action-btn text-red-600 hover:bg-red-50"
                title="Delete draft invoice"
              >
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
              </button>
              <!-- Force delete button for finalized invoices -->
              <button
                v-if="invoice.status !== 'Draft' && invoice.status !== 0"
                @click.prevent.stop="handleForceDelete(invoice)"
                class="action-btn text-orange-600 hover:bg-orange-50"
                title="Force delete finalized invoice"
              >
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="filteredInvoices.length > 0" class="summary-stats">
      <div class="stat-card">
        <p class="stat-label">Total Invoices</p>
        <p class="stat-value">{{ filteredInvoices.length }}</p>
      </div>
      <div class="stat-card">
        <p class="stat-label">Total Revenue</p>
        <p class="stat-value">{{ formatMoney(totalRevenue) }}</p>
      </div>
      <div class="stat-card">
        <p class="stat-label">Paid</p>
        <p class="stat-value">{{ paidCount }}</p>
      </div>
      <div class="stat-card">
        <p class="stat-label">Outstanding</p>
        <p class="stat-value">{{ outstandingCount }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useInvoicesStore } from '@/stores/invoices'
import { useCustomersStore } from '@/stores/customers'
import { InvoiceTypeNames, InvoiceStatusNames, InvoiceType, InvoiceStatus } from '@/types'
import type { InvoiceDto } from '@/types'

const router = useRouter()
const invoicesStore = useInvoicesStore()
const customersStore = useCustomersStore()

const searchQuery = ref('')
const statusFilter = ref('')
const typeFilter = ref('')

const filteredInvoices = computed(() => {
  let result = invoicesStore.invoices

  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(invoice =>
      invoice.invoiceNumber.toLowerCase().includes(query) ||
      getCustomerName(invoice.customerId).toLowerCase().includes(query)
    )
  }

  if (statusFilter.value) {
    result = result.filter(invoice => {
      // Backend returns string values like "Draft", "Finalized"
      if (typeof invoice.status === 'string') {
        return invoice.status === statusFilter.value
      }
      // Handle numeric enum values
      const statusNum = Object.entries(InvoiceStatusNames).find(
        ([_, name]) => name === statusFilter.value
      )?.[0]
      return statusNum !== undefined && invoice.status === Number(statusNum)
    })
  }

  if (typeFilter.value) {
    result = result.filter(invoice => {
      // Backend returns string values like "Monthly", "OneTime"
      if (typeof invoice.type === 'string') {
        // Handle "One-Time" filter matching "OneTime" backend value
        const backendValue = typeFilter.value === 'One-Time' ? 'OneTime' : typeFilter.value
        return invoice.type === backendValue
      }
      // Handle numeric enum values
      const typeNum = Object.entries(InvoiceTypeNames).find(
        ([_, name]) => name === typeFilter.value
      )?.[0]
      return typeNum !== undefined && invoice.type === Number(typeNum)
    })
  }

  return result.sort((a, b) => new Date(b.issueDate).getTime() - new Date(a.issueDate).getTime())
})

const totalRevenue = computed(() => {
  return filteredInvoices.value
    .filter(inv => inv.status === 'Paid' || inv.status === InvoiceStatus.Paid)
    .reduce((sum, inv) => sum + inv.total.amount, 0)
})

const paidCount = computed(() =>
  filteredInvoices.value.filter(inv => inv.status === 'Paid' || inv.status === InvoiceStatus.Paid).length
)

const outstandingCount = computed(() =>
  filteredInvoices.value.filter(inv => 
    inv.status === 'Finalized' || inv.status === 'Sent' ||
    inv.status === InvoiceStatus.Finalized || inv.status === InvoiceStatus.Sent
  ).length
)

onMounted(() => {
  loadData()
})

async function loadData() {
  await Promise.all([
    invoicesStore.fetchAll(),
    customersStore.fetchAll()
  ])
  console.log('Invoices loaded:', invoicesStore.invoices.length);
  if (invoicesStore.invoices.length > 0) {
    console.log('First invoice:', JSON.stringify(invoicesStore.invoices[0], null, 2));
  }
}

function getCustomerName(customerId: number): string {
  const customer = customersStore.getCustomerById(customerId)
  return customer?.name || 'Unknown'
}

function formatDate(date: string): string {
  return new Date(date).toLocaleDateString()
}

function formatMoney(money: { amount: number; currency: string } | undefined): string {
  if (!money || money.amount === undefined) {
    return '0.00 N/A'
  }
  return `${money.amount.toFixed(2)} ${money.currency}`
}

function formatType(type: InvoiceType | string): string {
  console.log('formatType called with:', type, 'Type:', typeof type);
  
  // Handle string enum values from backend
  if (typeof type === 'string') {
    // Backend returns "Monthly", "OneTime", etc as strings
    return type === 'OneTime' ? 'One-Time' : type;
  }
  
  // Handle numeric enum values
  const result = InvoiceTypeNames[type as InvoiceType];
  console.log('Result:', result);
  return result || 'Unknown'
}

function formatStatus(status: InvoiceStatus | string): string {
  console.log('formatStatus called with:', status, 'Type:', typeof status);
  
  // Handle string enum values from backend
  if (typeof status === 'string') {
    return status;
  }
  
  // Handle numeric enum values
  const result = InvoiceStatusNames[status as InvoiceStatus];
  console.log('Result:', result);
  return result || 'Unknown'
}

function getTypeCssClass(type: InvoiceType): string {
  return InvoiceTypeNames[type]?.toLowerCase().replace('-', '') || 'unknown'
}

function getStatusCssClass(status: InvoiceStatus): string {
  return InvoiceStatusNames[status]?.toLowerCase() || 'unknown'
}

function handleGenerate() {
  router.push('/invoices/new')
}

function handleView(id: number) {
  router.push(`/invoices/${id}`)
}

async function handleDownloadPdf(id: number) {
  try {
    await invoicesStore.downloadPdf(id)
  } catch (error: any) {
    alert(`Failed to download PDF: ${error.message}`)
  }
}

async function handleDelete(invoice: InvoiceDto) {
  console.log('handleDelete called for invoice:', invoice.id, 'Status:', invoice.status);
  
  if (!confirm(`Are you sure you want to delete invoice "${invoice.invoiceNumber}"?\n\nThis action cannot be undone.`)) {
    console.log('Delete cancelled by user');
    return
  }

  try {
    console.log('Calling invoicesStore.remove with force=false');
    await invoicesStore.remove(invoice.id, false)
    console.log('Delete successful');
  } catch (error: any) {
    console.error('Delete failed:', error);
    alert(`Failed to delete invoice: ${error.message}`)
  }
}

async function handleForceDelete(invoice: InvoiceDto) {
  console.log('handleForceDelete called for invoice:', invoice.id, 'Status:', invoice.status);
  
  if (!confirm(
    `⚠️ WARNING: Force Delete\n\n` +
    `You are about to force delete invoice "${invoice.invoiceNumber}".\n\n` +
    `This invoice is ${formatStatus(invoice.status)} and normally cannot be deleted.\n\n` +
    `Force delete should only be used to fix generation errors.\n\n` +
    `This action cannot be undone. Are you sure?`
  )) {
    console.log('Force delete cancelled by user');
    return
  }

  try {
    console.log('Calling invoicesStore.remove with force=true');
    await invoicesStore.remove(invoice.id, true)
    console.log('Force delete successful');
  } catch (error: any) {
    console.error('Force delete failed:', error);
    alert(`Failed to delete invoice: ${error.message}`)
  }
}
</script>

<style scoped>
.invoices-list {
  padding: 2rem;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.filters {
  display: flex;
  gap: 1rem;
  margin-bottom: 2rem;
}

.search-input,
.filter-select {
  padding: 0.75rem 1rem;
  border: 1px solid #d1d5db;
  border-radius: 0.5rem;
  font-size: 1rem;
}

.search-input {
  flex: 1;
  max-width: 400px;
}

.filter-select {
  min-width: 150px;
}

.search-input:focus,
.filter-select:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.btn-primary {
  display: inline-flex;
  align-items: center;
  padding: 0.75rem 1.5rem;
  background: #2563eb;
  color: white;
  border: none;
  border-radius: 0.375rem;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-primary:hover {
  background: #1d4ed8;
}

.loading-state,
.error-state,
.empty-state {
  text-align: center;
  padding: 4rem 2rem;
}

.spinner {
  border: 4px solid #f3f4f6;
  border-top: 4px solid #2563eb;
  border-radius: 50%;
  width: 40px;
  height: 40px;
  animation: spin 1s linear infinite;
  margin: 0 auto;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.invoices-table-container {
  background: white;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  overflow: hidden;
  margin-bottom: 2rem;
}

.invoices-table {
  width: 100%;
  border-collapse: collapse;
}

.invoices-table thead {
  background: #f9fafb;
  border-bottom: 2px solid #e5e7eb;
}

.invoices-table th {
  padding: 1rem;
  text-align: left;
  font-weight: 600;
  color: #374151;
  font-size: 0.875rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.invoices-table td {
  padding: 1rem;
  border-bottom: 1px solid #f3f4f6;
}

.invoice-row {
  cursor: pointer;
  transition: background 0.2s;
}

.invoice-row:hover {
  background: #f9fafb;
}

.type-badge,
.status-badge {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.type-monthly {
  background: #dbeafe;
  color: #1e40af;
}

.type-onetime {
  background: #e0e7ff;
  color: #4338ca;
}

.status-draft {
  background: #f3f4f6;
  color: #4b5563;
}

.status-finalized {
  background: #dbeafe;
  color: #1e40af;
}

.status-sent {
  background: #fef3c7;
  color: #92400e;
}

.status-paid {
  background: #d1fae5;
  color: #065f46;
}

.status-cancelled {
  background: #fee2e2;
  color: #991b1b;
}

.actions-cell {
  display: flex;
  gap: 0.5rem;
}

.action-btn {
  padding: 0.5rem;
  border: none;
  background: transparent;
  border-radius: 0.375rem;
  cursor: pointer;
  color: #6b7280;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.action-btn svg {
  width: 1.25rem;
  height: 1.25rem;
  stroke: currentColor;
}

.action-btn:hover {
  background: #f3f4f6;
  color: #1f2937;
}

.summary-stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1.5rem;
}

.stat-card {
  background: white;
  padding: 1.5rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.stat-label {
  font-size: 0.875rem;
  color: #6b7280;
  margin-bottom: 0.5rem;
}

.stat-value {
  font-size: 1.5rem;
  font-weight: 700;
  color: #1f2937;
}
</style>