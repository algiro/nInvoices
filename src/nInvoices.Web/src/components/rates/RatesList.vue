<template>
  <div class="rates-list">
    <div class="list-header">
      <h3 class="text-lg font-semibold">Rates</h3>
      <button @click="handleAdd" class="btn-primary">
        <svg class="w-4 h-4 inline mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        Add Rate
      </button>
    </div>

    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <p class="mt-2 text-sm text-gray-600">Loading rates...</p>
    </div>

    <div v-else-if="error" class="error-state">
      <p class="text-red-600 text-sm">{{ error }}</p>
      <button @click="loadRates" class="btn-secondary mt-2">Retry</button>
    </div>

    <div v-else-if="rates.length === 0" class="empty-state">
      <p class="text-gray-600 text-sm">No rates configured yet</p>
      <button @click="handleAdd" class="btn-primary mt-2">Add First Rate</button>
    </div>

    <div v-else class="rates-grid">
      <div v-for="rate in rates" :key="rate.id" class="rate-card">
        <div class="rate-header">
          <span class="rate-type-badge" :class="`type-${rate.type.toLowerCase()}`">
            {{ rate.type }}
          </span>
          <div class="rate-actions">
            <button
              @click="handleEdit(rate)"
              class="action-btn"
              title="Edit rate"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
              </svg>
            </button>
            <button
              @click="handleDelete(rate)"
              class="action-btn text-red-600 hover:bg-red-50"
              title="Delete rate"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
            </button>
          </div>
        </div>
        <div class="rate-amount">
          {{ formatMoney(rate.price) }}
        </div>
        <div class="rate-description">
          per {{ rate.type.toLowerCase() }}
        </div>
      </div>
    </div>

    <teleport to="body">
      <div v-if="showForm" class="modal-overlay" @click="handleCloseForm">
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h3 class="text-xl font-semibold">{{ editingRate ? 'Edit Rate' : 'Add Rate' }}</h3>
            <button @click="handleCloseForm" class="close-btn">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
          <RateForm
            :customer-id="customerId"
            :rate-id="editingRate?.id"
            @success="handleFormSuccess"
            @cancel="handleCloseForm"
          />
        </div>
      </div>
    </teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRatesStore } from '@/stores/rates'
import RateForm from './RateForm.vue'
import type { RateDto, MoneyDto } from '@/types'

interface Props {
  customerId: number
}

const props = defineProps<Props>()
const ratesStore = useRatesStore()

const showForm = ref(false)
const editingRate = ref<RateDto | null>(null)

const rates = computed(() => ratesStore.ratesByCustomer(props.customerId))
const loading = computed(() => ratesStore.loading)
const error = computed(() => ratesStore.error)

onMounted(() => {
  loadRates()
})

async function loadRates() {
  try {
    await ratesStore.fetchByCustomerId(props.customerId)
  } catch (error) {
    console.error('Failed to load rates:', error)
  }
}

function formatMoney(money: MoneyDto): string {
  return `${money.amount.toFixed(2)} ${money.currency}`
}

function handleAdd() {
  editingRate.value = null
  showForm.value = true
}

function handleEdit(rate: RateDto) {
  editingRate.value = rate
  showForm.value = true
}

async function handleDelete(rate: RateDto) {
  if (!confirm(`Are you sure you want to delete this ${rate.type.toLowerCase()} rate?\n\nThis action cannot be undone.`)) {
    return
  }

  try {
    await ratesStore.remove(rate.id)
  } catch (error: any) {
    alert(`Failed to delete rate: ${error.message}`)
  }
}

function handleFormSuccess() {
  showForm.value = false
  editingRate.value = null
  loadRates()
}

function handleCloseForm() {
  showForm.value = false
  editingRate.value = null
}
</script>

<style scoped>
.rates-list {
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

.rates-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1rem;
}

.rate-card {
  background: #f9fafb;
  padding: 1.5rem;
  border-radius: 0.5rem;
  border: 1px solid #e5e7eb;
  transition: all 0.2s;
}

.rate-card:hover {
  border-color: #2563eb;
  box-shadow: 0 2px 8px rgba(37, 99, 235, 0.1);
}

.rate-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.rate-type-badge {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.type-daily {
  background: #dbeafe;
  color: #1e40af;
}

.type-monthly {
  background: #d1fae5;
  color: #065f46;
}

.type-hourly {
  background: #fef3c7;
  color: #92400e;
}

.rate-actions {
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
  background: white;
  color: #1f2937;
}

.rate-amount {
  font-size: 1.5rem;
  font-weight: 700;
  color: #1f2937;
  margin-bottom: 0.25rem;
}

.rate-description {
  font-size: 0.875rem;
  color: #6b7280;
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
  max-width: 500px;
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