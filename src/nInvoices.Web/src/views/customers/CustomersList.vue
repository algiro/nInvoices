<template>
  <div class="customers-list">
    <div class="header">
      <h1 class="text-3xl font-bold">Customers</h1>
      <button @click="handleCreate" class="btn-primary">
        <svg class="w-5 h-5 inline mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        Add Customer
      </button>
    </div>

    <div class="search-bar">
      <input
        v-model="searchQuery"
        type="text"
        placeholder="Search customers by name, fiscal ID, or city..."
        class="search-input"
      />
    </div>

    <div v-if="store.loading" class="loading-state">
      <div class="spinner"></div>
      <p class="mt-4">Loading customers...</p>
    </div>

    <div v-else-if="store.error" class="error-state">
      <p class="text-red-600">{{ store.error }}</p>
      <button @click="loadData" class="btn-primary mt-4">Retry</button>
    </div>

    <div v-else-if="filteredCustomers.length === 0" class="empty-state">
      <svg class="w-16 h-16 mx-auto text-gray-400 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
      </svg>
      <p class="text-xl text-gray-600 mb-2">
        {{ searchQuery ? 'No customers found' : 'No customers yet' }}
      </p>
      <p class="text-gray-500 mb-4">
        {{ searchQuery ? 'Try adjusting your search criteria' : 'Get started by adding your first customer' }}
      </p>
      <button v-if="!searchQuery" @click="handleCreate" class="btn-primary">Add Your First Customer</button>
    </div>

    <div v-else class="customers-grid">
      <div
        v-for="customer in filteredCustomers"
        :key="customer.id"
        class="customer-card"
        @click="handleView(customer.id)"
      >
        <div class="card-header">
          <div>
            <h3 class="text-xl font-semibold">{{ customer.name }}</h3>
            <p class="text-gray-600 text-sm">{{ customer.fiscalId }}</p>
          </div>
          <div class="card-actions" @click.stop>
            <button
              @click="handleEdit(customer.id)"
              class="action-btn"
              title="Edit customer"
            >
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
              </svg>
            </button>
            <button
              @click="handleDelete(customer)"
              class="action-btn text-red-600 hover:bg-red-50"
              title="Delete customer"
            >
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
            </button>
          </div>
        </div>

        <div class="card-body">
          <div class="address-info">
            <svg class="w-4 h-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            <p class="text-sm text-gray-500">
              {{ customer.address.city }}, {{ customer.address.country }}
            </p>
          </div>
        </div>

        <div class="card-footer">
          <span class="view-details">View Details →</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useCustomersStore } from '@/stores/customers'
import type { CustomerDto } from '@/types'

const router = useRouter()
const store = useCustomersStore()
const searchQuery = ref('')

const filteredCustomers = computed(() => {
  if (!searchQuery.value) {
    return store.customers
  }

  const query = searchQuery.value.toLowerCase()
  return store.customers.filter(customer =>
    customer.name.toLowerCase().includes(query) ||
    customer.fiscalId.toLowerCase().includes(query) ||
    customer.address.city.toLowerCase().includes(query) ||
    customer.address.country.toLowerCase().includes(query)
  )
})

onMounted(() => {
  loadData()
})

function loadData() {
  store.fetchAll()
}

function handleCreate() {
  router.push('/customers/new')
}

function handleView(id: number) {
  router.push(`/customers/${id}`)
}

function handleEdit(id: number) {
  router.push(`/customers/${id}/edit`)
}

async function handleDelete(customer: CustomerDto) {
  if (!confirm(`Are you sure you want to delete customer "${customer.name}"?\n\nThis action cannot be undone.`)) {
    return
  }

  try {
    await store.remove(customer.id)
  } catch (error: any) {
    alert(`Failed to delete customer: ${error.message}`)
  }
}
</script>

<style scoped>
.customers-list {
  padding: 2rem;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.search-bar {
  margin-bottom: 2rem;
}

.search-input {
  width: 100%;
  max-width: 500px;
  padding: 0.75rem 1rem;
  border: 1px solid #d1d5db;
  border-radius: 0.5rem;
  font-size: 1rem;
}

.search-input:focus {
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

.customers-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1.5rem;
}

.customer-card {
  background: white;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  transition: all 0.2s;
  cursor: pointer;
  overflow: hidden;
}

.customer-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  transform: translateY(-2px);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: start;
  padding: 1.5rem 1.5rem 1rem;
  border-bottom: 1px solid #f3f4f6;
}

.card-actions {
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
}

.action-btn:hover {
  background: #f3f4f6;
  color: #1f2937;
}

.card-body {
  padding: 1rem 1.5rem;
}

.address-info {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.card-footer {
  padding: 1rem 1.5rem;
  background: #f9fafb;
  text-align: right;
}

.view-details {
  color: #2563eb;
  font-size: 0.875rem;
  font-weight: 500;
}
</style>
