<template>
  <div class="invoice-details">
    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <p class="mt-4">Loading invoice...</p>
    </div>

    <div v-else-if="error" class="error-state">
      <p class="text-red-600">{{ error }}</p>
      <button @click="loadData" class="btn-primary mt-4">Retry</button>
    </div>

    <div v-else-if="invoice" class="invoice-content">
      <div class="invoice-header">
        <div>
          <h1 class="text-3xl font-bold">Invoice {{ invoice.invoiceNumber }}</h1>
          <p class="text-gray-600 mt-2">
            <span class="status-badge" :class="`status-${getStatusCssClass(invoice.status)}`">
              {{ formatStatus(invoice.status) }}
            </span>
          </p>
        </div>
        <div class="header-actions">
          <button
            @click="handleDownloadPdf"
            class="btn-primary"
            :disabled="downloadingPdf"
          >
            <svg class="w-5 h-5 inline mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            {{ downloadingPdf ? 'Downloading...' : 'Download PDF' }}
          </button>
          <button
            v-if="invoice && (invoice.type === 'Monthly' || invoice.type === 0)"
            @click="handleDownloadMonthlyReport"
            class="btn-primary"
            :disabled="downloadingMonthlyReport"
          >
            <svg class="w-5 h-5 inline mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            {{ downloadingMonthlyReport ? 'Downloading...' : 'Download Monthly Report' }}
          </button>
          <button
            @click="handleRegenerateInvoicePdf"
            class="btn-secondary"
            :disabled="regeneratingInvoice"
            title="Regenerate PDF with current template"
          >
            <svg class="w-5 h-5 inline mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {{ regeneratingInvoice ? 'Regenerating...' : 'Regenerate Invoice PDF' }}
          </button>
          <button
            v-if="invoice && (invoice.type === 'Monthly' || invoice.type === 0)"
            @click="handleRegenerateMonthlyReport"
            class="btn-secondary"
            :disabled="regeneratingMonthlyReport"
            title="Verify monthly report template"
          >
            <svg class="w-5 h-5 inline mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {{ regeneratingMonthlyReport ? 'Verifying...' : 'Verify Monthly Report' }}
          </button>
          <button
            v-if="invoice.status === InvoiceStatus.Draft"
            @click="handleFinalize"
            class="btn-secondary"
          >
            Finalize
          </button>
          <button
            v-if="invoice.status === InvoiceStatus.Draft"
            @click="handleDelete"
            class="btn-danger"
          >
            Delete
          </button>
        </div>
      </div>

      <div class="invoice-body">
        <div class="info-section">
          <div class="info-card">
            <h3 class="card-title">Customer Information</h3>
            <dl class="info-list">
              <div>
                <dt>Name</dt>
                <dd>{{ customerName }}</dd>
              </div>
              <div>
                <dt>Customer ID</dt>
                <dd>{{ invoice.customerId }}</dd>
              </div>
            </dl>
          </div>

          <div class="info-card">
            <h3 class="card-title">Invoice Details</h3>
            <dl class="info-list">
              <div>
                <dt>Type</dt>
                <dd>{{ formatType(invoice.type) }}</dd>
              </div>
              <div>
                <dt>Issue Date</dt>
                <dd>{{ formatDate(invoice.issueDate) }}</dd>
              </div>
              <div v-if="invoice.dueDate">
                <dt>Due Date</dt>
                <dd>{{ formatDate(invoice.dueDate) }}</dd>
              </div>
            </dl>
          </div>
        </div>

        <div v-if="invoice.type === 'Monthly' && invoice.monthlyDetails" class="monthly-details">
          <h3 class="section-title">Monthly Details</h3>
          <div class="details-grid">
            <div class="detail-item">
              <span class="detail-label">Month</span>
              <span class="detail-value">{{ invoice.monthlyDetails.monthNumber }}</span>
            </div>
            <div class="detail-item">
              <span class="detail-label">Year</span>
              <span class="detail-value">{{ invoice.monthlyDetails.year }}</span>
            </div>
            <div class="detail-item">
              <span class="detail-label">Worked Days</span>
              <span class="detail-value">{{ invoice.monthlyDetails.workedDays }}</span>
            </div>
          </div>
        </div>

        <div v-if="invoice.expenses && invoice.expenses.length > 0" class="expenses-section">
          <h3 class="section-title">Expenses</h3>
          <table class="expenses-table">
            <thead>
              <tr>
                <th>Description</th>
                <th class="text-right">Amount</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(expense, index) in invoice.expenses" :key="index">
                <td>{{ expense.description }}</td>
                <td class="text-right">{{ formatMoney(expense.price) }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-if="invoice.taxLines && invoice.taxLines.length > 0" class="tax-section">
          <h3 class="section-title">Tax Breakdown</h3>
          <table class="tax-table">
            <thead>
              <tr>
                <th>Tax</th>
                <th>Rate</th>
                <th class="text-right">Amount</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(taxLine, index) in invoice.taxLines" :key="index">
                <td>{{ taxLine.taxDescription }}</td>
                <td>{{ formatTaxRate(taxLine.rate) }}</td>
                <td class="text-right">{{ formatMoney(taxLine.amount) }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="totals-section">
          <div class="totals-card">
            <div class="total-row">
              <span class="total-label">Subtotal</span>
              <span class="total-value">{{ formatMoney(invoice.subtotal) }}</span>
            </div>
            <div v-if="invoice.totalTax" class="total-row">
              <span class="total-label">Total Tax</span>
              <span class="total-value">{{ formatMoney(invoice.totalTax) }}</span>
            </div>
            <div class="total-row final">
              <span class="total-label">Total</span>
              <span class="total-value">{{ formatMoney(invoice.total) }}</span>
            </div>
          </div>
        </div>

        <div v-if="invoice.notes" class="notes-section">
          <h3 class="section-title">Notes</h3>
          <p class="notes-content">{{ invoice.notes }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useInvoicesStore } from '@/stores/invoices'
import { useCustomersStore } from '@/stores/customers'
import { InvoiceStatusNames, InvoiceTypeNames, InvoiceStatus, InvoiceType } from '@/types'
import type { MoneyDto } from '@/types'

const route = useRoute()
const router = useRouter()
const invoicesStore = useInvoicesStore()
const customersStore = useCustomersStore()

const invoiceId = computed(() => Number(route.params.id))
const invoice = computed(() => invoicesStore.selectedInvoice)
const loading = ref(false)
const error = ref<string | null>(null)
const downloadingPdf = ref(false)
const downloadingMonthlyReport = ref(false)
const regeneratingInvoice = ref(false)
const regeneratingMonthlyReport = ref(false)

const customerName = computed(() => {
  if (!invoice.value) return ''
  const customer = customersStore.getCustomerById(invoice.value.customerId)
  return customer?.name || 'Unknown'
})

onMounted(() => {
  loadData()
})

async function loadData() {
  try {
    loading.value = true
    error.value = null
    await Promise.all([
      invoicesStore.fetchById(invoiceId.value),
      customersStore.fetchAll()
    ])
  } catch (err: any) {
    error.value = err.message || 'Failed to load invoice'
  } finally {
    loading.value = false
  }
}

function formatDate(date: string): string {
  return new Date(date).toLocaleDateString()
}

function formatMoney(money: MoneyDto | undefined): string {
  if (!money || money.amount === undefined) {
    return '0.00 N/A'
  }
  return `${money.amount.toFixed(2)} ${money.currency}`
}

function formatType(type: InvoiceType): string {
  return InvoiceTypeNames[type] || 'Unknown'
}

function formatStatus(status: InvoiceStatus): string {
  return InvoiceStatusNames[status] || 'Unknown'
}

function getStatusCssClass(status: InvoiceStatus): string {
  return InvoiceStatusNames[status]?.toLowerCase() || 'unknown'
}

function formatTaxRate(rate: number): string {
  return `${rate.toFixed(2)}%`
}

async function handleDownloadPdf() {
  try {
    downloadingPdf.value = true
    await invoicesStore.downloadPdf(invoiceId.value)
  } catch (err: any) {
    alert(`Failed to download PDF: ${err.message}`)
  } finally {
    downloadingPdf.value = false
  }
}

async function handleDownloadMonthlyReport() {
  try {
    downloadingMonthlyReport.value = true
    await invoicesStore.downloadMonthlyReportPdf(invoiceId.value)
  } catch (err: any) {
    alert(`Failed to download monthly report: ${err.message}`)
  } finally {
    downloadingMonthlyReport.value = false
  }
}

async function handleRegenerateInvoicePdf() {
  if (!confirm('Regenerate invoice PDF with current template? This will update the rendered content.')) {
    return
  }

  try {
    regeneratingInvoice.value = true
    const result = await invoicesStore.regenerateInvoicePdf(invoiceId.value)
    alert(result.message || 'Invoice PDF regenerated successfully!')
    await loadData()
  } catch (err: any) {
    alert(`Failed to regenerate invoice PDF: ${err.message}`)
  } finally {
    regeneratingInvoice.value = false
  }
}

async function handleRegenerateMonthlyReport() {
  try {
    regeneratingMonthlyReport.value = true
    const result = await invoicesStore.regenerateMonthlyReportPdf(invoiceId.value)
    alert(result.message || 'Monthly report verified successfully!')
  } catch (err: any) {
    alert(`Failed to verify monthly report: ${err.message}`)
  } finally {
    regeneratingMonthlyReport.value = false
  }
}

async function handleFinalize() {
  if (!confirm('Are you sure you want to finalize this invoice? This action cannot be undone.')) {
    return
  }

  try {
    await invoicesStore.finalize(invoiceId.value)
    await loadData()
  } catch (err: any) {
    alert(`Failed to finalize invoice: ${err.message}`)
  }
}

async function handleDelete() {
  if (!confirm('Are you sure you want to delete this invoice? This action cannot be undone.')) {
    return
  }

  try {
    await invoicesStore.remove(invoiceId.value)
    router.push('/invoices')
  } catch (err: any) {
    alert(`Failed to delete invoice: ${err.message}`)
  }
}
</script>

<style scoped>
.invoice-details {
  padding: 2rem;
  max-width: 1200px;
  margin: 0 auto;
}

.loading-state,
.error-state {
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

.invoice-header {
  display: flex;
  justify-content: space-between;
  align-items: start;
  background: white;
  padding: 2rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  margin-bottom: 2rem;
}

.header-actions {
  display: flex;
  gap: 1rem;
}

.status-badge {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
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

.invoice-body {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.info-section {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1.5rem;
}

.info-card {
  background: white;
  padding: 1.5rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.card-title {
  font-size: 1.125rem;
  font-weight: 600;
  margin-bottom: 1rem;
  color: #1f2937;
}

.info-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.info-list dt {
  font-size: 0.875rem;
  color: #6b7280;
}

.info-list dd {
  font-weight: 500;
  color: #1f2937;
}

.monthly-details,
.expenses-section,
.tax-section,
.notes-section {
  background: white;
  padding: 1.5rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.section-title {
  font-size: 1.125rem;
  font-weight: 600;
  margin-bottom: 1rem;
  color: #1f2937;
}

.details-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 1rem;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.detail-label {
  font-size: 0.875rem;
  color: #6b7280;
}

.detail-value {
  font-size: 1.25rem;
  font-weight: 600;
  color: #1f2937;
}

.expenses-table,
.tax-table {
  width: 100%;
  border-collapse: collapse;
}

.expenses-table thead,
.tax-table thead {
  background: #f9fafb;
}

.expenses-table th,
.tax-table th,
.expenses-table td,
.tax-table td {
  padding: 0.75rem;
  text-align: left;
}

.expenses-table tbody tr,
.tax-table tbody tr {
  border-bottom: 1px solid #f3f4f6;
}

.totals-section {
  background: white;
  padding: 1.5rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.totals-card {
  max-width: 400px;
  margin-left: auto;
}

.total-row {
  display: flex;
  justify-content: space-between;
  padding: 0.75rem 0;
  border-bottom: 1px solid #f3f4f6;
}

.total-row.final {
  border-bottom: none;
  border-top: 2px solid #1f2937;
  padding-top: 1rem;
  margin-top: 0.5rem;
}

.total-label {
  font-weight: 500;
  color: #6b7280;
}

.total-row.final .total-label {
  font-size: 1.125rem;
  font-weight: 700;
  color: #1f2937;
}

.total-value {
  font-weight: 600;
  color: #1f2937;
}

.total-row.final .total-value {
  font-size: 1.5rem;
  font-weight: 700;
  color: #2563eb;
}

.notes-content {
  color: #4b5563;
  line-height: 1.6;
  white-space: pre-wrap;
}

.btn-primary,
.btn-secondary,
.btn-danger {
  padding: 0.75rem 1.5rem;
  border-radius: 0.375rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
}

.btn-primary {
  background: #2563eb;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #1d4ed8;
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: white;
  color: #374151;
  border: 1px solid #d1d5db;
}

.btn-secondary:hover {
  background: #f9fafb;
}

.btn-danger {
  background: #ef4444;
  color: white;
}

.btn-danger:hover {
  background: #dc2626;
}
</style>