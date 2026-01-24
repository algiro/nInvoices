<template>
  <div class="customer-details">
    <div v-if="loading" class="loading-state">
      <p>Loading customer details...</p>
    </div>

    <div v-else-if="error" class="error-state">
      <p class="text-red-600">{{ error }}</p>
      <button @click="loadData" class="btn-primary mt-4">Retry</button>
    </div>

    <div v-else-if="customer" class="customer-content">
      <div class="customer-header">
        <div class="flex justify-between items-start">
          <div>
            <h1 class="text-3xl font-bold">{{ customer.name }}</h1>
            <p class="text-gray-600 mt-1">Fiscal ID: {{ customer.fiscalId }}</p>
            <p class="text-gray-600 mt-1">
              {{ formatAddress(customer.address) }}
            </p>
          </div>
          <div class="flex gap-2">
            <button
              @click="handleEdit"
              class="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition"
            >
              Edit
            </button>
            <button
              @click="handleDelete"
              class="px-4 py-2 bg-red-600 text-white rounded-md hover:bg-red-700 transition"
            >
              Delete
            </button>
          </div>
        </div>
      </div>

      <div class="tabs">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          @click="activeTab = tab.id"
          class="tab-button"
          :class="{ active: activeTab === tab.id }"
        >
          {{ tab.label }}
        </button>
      </div>

      <div class="tab-content">
        <div v-if="activeTab === 'overview'" class="tab-pane">
          <div class="grid grid-cols-2 gap-6">
            <div class="info-card">
              <h3 class="text-lg font-semibold mb-3">Contact Information</h3>
              <dl class="space-y-2">
                <div>
                  <dt class="text-sm text-gray-600">Name</dt>
                  <dd class="font-medium">{{ customer.name }}</dd>
                </div>
                <div>
                  <dt class="text-sm text-gray-600">Fiscal ID</dt>
                  <dd class="font-medium">{{ customer.fiscalId }}</dd>
                </div>
              </dl>
            </div>

            <div class="info-card">
              <h3 class="text-lg font-semibold mb-3">Address</h3>
              <address class="not-italic text-gray-700">
                {{ customer.address.street }} {{ customer.address.houseNumber }}<br>
                {{ customer.address.zipCode }} {{ customer.address.city }}<br>
                <span v-if="customer.address.state">{{ customer.address.state }}<br></span>
                {{ customer.address.country }}
              </address>
            </div>
          </div>
        </div>

        <div v-if="activeTab === 'rates'" class="tab-pane">
          <RatesList :customer-id="customerId" />
        </div>

        <div v-if="activeTab === 'taxes'" class="tab-pane">
          <TaxesList :customer-id="customerId" />
        </div>

        <div v-if="activeTab === 'templates'" class="tab-pane">
          <TemplatesList :customer-id="customerId" />
        </div>

        <div v-if="activeTab === 'monthly-reports'" class="tab-pane">
          <MonthlyReportTemplatesList :customer-id="customerId" />
        </div>

        <div v-if="activeTab === 'invoices'" class="tab-pane">
          <div class="flex justify-between items-center mb-4">
            <h3 class="text-lg font-semibold">Invoices</h3>
            <button class="btn-primary">Generate Invoice</button>
          </div>
          <p class="text-gray-600">Invoice history coming soon...</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useCustomersStore } from '@/stores/customers'
import RatesList from '@/components/rates/RatesList.vue'
import TaxesList from '@/components/taxes/TaxesList.vue'
import TemplatesList from '@/components/templates/TemplatesList.vue'
import MonthlyReportTemplatesList from '@/components/monthlyreports/MonthlyReportTemplatesList.vue'
import type { AddressDto } from '@/types'

const route = useRoute()
const router = useRouter()
const customersStore = useCustomersStore()

const customerId = computed(() => Number(route.params.id))
const customer = computed(() => customersStore.selectedCustomer)
const loading = ref(false)
const error = ref<string | null>(null)
const activeTab = ref('overview')

const tabs = [
  { id: 'overview', label: 'Overview' },
  { id: 'rates', label: 'Rates' },
  { id: 'taxes', label: 'Taxes' },
  { id: 'templates', label: 'Invoice Templates' },
  { id: 'monthly-reports', label: 'Monthly Reports' },
  { id: 'invoices', label: 'Invoices' }
]

onMounted(() => {
  loadData()
})

async function loadData() {
  try {
    loading.value = true
    error.value = null
    await customersStore.fetchById(customerId.value)
  } catch (err: any) {
    error.value = err.message || 'Failed to load customer'
  } finally {
    loading.value = false
  }
}

function formatAddress(address: AddressDto): string {
  const parts = [
    `${address.street} ${address.houseNumber}`,
    `${address.zipCode} ${address.city}`,
    address.state,
    address.country
  ].filter(Boolean)
  return parts.join(', ')
}

function handleEdit() {
  router.push(`/customers/${customerId.value}/edit`)
}

async function handleDelete() {
  if (!confirm(`Are you sure you want to delete customer "${customer.value?.name}"?`)) {
    return
  }

  try {
    await customersStore.remove(customerId.value)
    router.push('/customers')
  } catch (err: any) {
    error.value = err.message || 'Failed to delete customer'
  }
}
</script>

<style scoped>
.customer-details {
  padding: 2rem;
}

.loading-state,
.error-state {
  text-align: center;
  padding: 4rem 2rem;
}

.customer-header {
  background: white;
  padding: 2rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  margin-bottom: 2rem;
}

.tabs {
  display: flex;
  gap: 0.5rem;
  border-bottom: 2px solid #e5e7eb;
  margin-bottom: 2rem;
}

.tab-button {
  padding: 0.75rem 1.5rem;
  border: none;
  background: none;
  cursor: pointer;
  font-weight: 500;
  color: #6b7280;
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  transition: all 0.2s;
}

.tab-button:hover {
  color: #2563eb;
}

.tab-button.active {
  color: #2563eb;
  border-bottom-color: #2563eb;
}

.tab-content {
  background: white;
  padding: 2rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.info-card {
  background: #f9fafb;
  padding: 1.5rem;
  border-radius: 0.5rem;
}

.btn-primary {
  padding: 0.5rem 1rem;
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
</style>