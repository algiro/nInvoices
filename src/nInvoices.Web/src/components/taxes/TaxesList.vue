<template>
  <div class="taxes-list">
    <div class="list-header">
      <h3 class="text-lg font-semibold">Tax Configuration</h3>
      <button @click="handleAdd" class="btn-primary">
        <svg class="w-4 h-4 inline mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        Add Tax
      </button>
    </div>

    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <p class="mt-2 text-sm text-gray-600">Loading taxes...</p>
    </div>

    <div v-else-if="error" class="error-state">
      <p class="text-red-600 text-sm">{{ error }}</p>
      <button @click="loadTaxes" class="btn-secondary mt-2">Retry</button>
    </div>

    <div v-else-if="taxes.length === 0" class="empty-state">
      <p class="text-gray-600 text-sm">No taxes configured yet</p>
      <button @click="handleAdd" class="btn-primary mt-2">Add First Tax</button>
    </div>

    <div v-else class="taxes-table-container">
      <table class="taxes-table">
        <thead>
          <tr>
            <th>Order</th>
            <th>Description</th>
            <th>Type</th>
            <th>Rate</th>
            <th>Applied To</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="tax in sortedTaxes" :key="tax.id">
            <td class="font-semibold">{{ tax.order }}</td>
            <td>{{ tax.description }}</td>
            <td>
              <span class="handler-badge" :class="`handler-${tax.handlerId.toLowerCase()}`">
                {{ formatHandlerName(tax.handlerId) }}
              </span>
            </td>
            <td>{{ tax.rate.toFixed(2) }}%</td>
            <td class="text-sm text-gray-600">
              {{ tax.appliedToTaxId ? getTaxName(tax.appliedToTaxId) : 'Subtotal' }}
            </td>
            <td>
              <div class="action-buttons">
                <button
                  @click="handleEdit(tax)"
                  class="action-btn"
                  title="Edit tax"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                </button>
                <button
                  @click="handleDelete(tax)"
                  class="action-btn text-red-600 hover:bg-red-50"
                  title="Delete tax"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <teleport to="body">
      <div v-if="showForm" class="modal-overlay" @click="handleCloseForm">
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h3 class="text-xl font-semibold">{{ editingTax ? 'Edit Tax' : 'Add Tax' }}</h3>
            <button @click="handleCloseForm" class="close-btn">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
          <TaxForm
            :customer-id="customerId"
            :tax-id="editingTax?.id"
            @success="handleFormSuccess"
            @cancel="handleCloseForm"
          />
        </div>
      </div>
    </teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useTaxesStore } from '@/stores/taxes'
import TaxForm from './TaxForm.vue'
import type { TaxDto } from '@/types'

interface Props {
  customerId: number
}

const props = defineProps<Props>()
const taxesStore = useTaxesStore()

const showForm = ref(false)
const editingTax = ref<TaxDto | null>(null)

const taxes = computed(() => taxesStore.taxesByCustomer(props.customerId))
const loading = computed(() => taxesStore.loading)
const error = computed(() => taxesStore.error)

const sortedTaxes = computed(() => {
  return [...taxes.value].sort((a, b) => a.order - b.order)
})

onMounted(() => {
  loadTaxes()
})

async function loadTaxes() {
  try {
    await taxesStore.fetchByCustomerId(props.customerId)
  } catch (error) {
    console.error('Failed to load taxes:', error)
  }
}

function formatHandlerName(handlerId: string): string {
  switch (handlerId) {
    case 'PERCENTAGE':
      return 'Percentage'
    case 'FIXED_AMOUNT':
      return 'Fixed Amount'
    case 'COMPOUND':
      return 'Compound'
    default:
      return handlerId
  }
}

function getTaxName(taxId: number | null): string {
  if (!taxId) return 'Subtotal'
  const tax = taxesStore.getTaxById(taxId)
  return tax ? tax.description : 'Unknown'
}

function handleAdd() {
  editingTax.value = null
  showForm.value = true
}

function handleEdit(tax: TaxDto) {
  editingTax.value = tax
  showForm.value = true
}

async function handleDelete(tax: TaxDto) {
  if (!confirm(`Are you sure you want to delete tax "${tax.description}"?\n\nThis action cannot be undone.`)) {
    return
  }

  try {
    await taxesStore.remove(tax.id)
  } catch (error: any) {
    alert(`Failed to delete tax: ${error.message}`)
  }
}

function handleFormSuccess() {
  showForm.value = false
  editingTax.value = null
  loadTaxes()
}

function handleCloseForm() {
  showForm.value = false
  editingTax.value = null
}
</script>

<style scoped>
.taxes-list {
  padding: 1rem;
}

.list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
}

.loading-state,
.error-state,
.empty-state {
  text-align: center;
  padding: 3rem 1rem;
}

.spinner {
  border: 3px solid #f3f4f6;
  border-top: 3px solid #2563eb;
  border-radius: 50%;
  width: 30px;
  height: 30px;
  animation: spin 1s linear infinite;
  margin: 0 auto;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.taxes-table-container {
  background: white;
  border-radius: 0.5rem;
  overflow: hidden;
  border: 1px solid #e5e7eb;
}

.taxes-table {
  width: 100%;
  border-collapse: collapse;
}

.taxes-table thead {
  background: #f9fafb;
}

.taxes-table th {
  padding: 0.75rem 1rem;
  text-align: left;
  font-weight: 600;
  color: #374151;
  font-size: 0.875rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  border-bottom: 2px solid #e5e7eb;
}

.taxes-table td {
  padding: 1rem;
  border-bottom: 1px solid #f3f4f6;
}

.taxes-table tbody tr:hover {
  background: #f9fafb;
}

.handler-badge {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.handler-percentage {
  background: #dbeafe;
  color: #1e40af;
}

.handler-fixed_amount {
  background: #fef3c7;
  color: #92400e;
}

.handler-compound {
  background: #e0e7ff;
  color: #4338ca;
}

.action-buttons {
  display: flex;
  gap: 0.5rem;
}

.action-btn {
  padding: 0.25rem;
  border: none;
  background: transparent;
  border-radius: 0.375rem;
  cursor: pointer;
  color: #6b7280;
  transition: all 0.2s;
}

.action-btn:hover {
  background: #f3f4f6;
  color: #1f2937;
}

.btn-primary,
.btn-secondary {
  padding: 0.5rem 1rem;
  border-radius: 0.375rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
  font-size: 0.875rem;
}

.btn-primary {
  background: #2563eb;
  color: white;
}

.btn-primary:hover {
  background: #1d4ed8;
}

.btn-secondary {
  background: white;
  color: #374151;
  border: 1px solid #d1d5db;
}

.btn-secondary:hover {
  background: #f9fafb;
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: white;
  border-radius: 0.5rem;
  max-width: 600px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  border-bottom: 1px solid #e5e7eb;
}

.close-btn {
  padding: 0.5rem;
  border: none;
  background: transparent;
  border-radius: 0.375rem;
  cursor: pointer;
  color: #6b7280;
  transition: all 0.2s;
}

.close-btn:hover {
  background: #f3f4f6;
  color: #1f2937;
}
</style>