<script setup lang="ts">
import { onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useCustomersStore } from '../../stores/customers';

const router = useRouter();
const store = useCustomersStore();

onMounted(() => {
  store.fetchAll();
});

const viewCustomer = (id: number) => {
  router.push({ name: 'customer-details', params: { id } });
};
</script>

<template>
  <div class="customers-list">
    <div class="header">
      <h1>Customers</h1>
      <router-link to="/customers/new" class="btn-primary">
        + New Customer
      </router-link>
    </div>

    <div v-if="store.loading" class="loading">Loading customers...</div>
    
    <div v-else-if="store.error" class="error">{{ store.error }}</div>

    <div v-else class="customers-grid">
      <div 
        v-for="customer in store.customers" 
        :key="customer.id"
        class="customer-card"
        @click="viewCustomer(customer.id)"
      >
        <h3>{{ customer.name }}</h3>
        <p class="fiscal-id">{{ customer.fiscalId }}</p>
        <p class="address">
          {{ customer.address.city }}, {{ customer.address.country }}
        </p>
      </div>

      <div v-if="store.customers.length === 0" class="empty">
        No customers yet. Create your first customer to get started!
      </div>
    </div>
  </div>
</template>

<style scoped>
.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.btn-primary {
  padding: 0.75rem 1.5rem;
  background: #3498db;
  color: white;
  text-decoration: none;
  border-radius: 6px;
  font-weight: 500;
}

.btn-primary:hover {
  background: #2980b9;
}

.customers-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.5rem;
}

.customer-card {
  background: white;
  padding: 1.5rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  cursor: pointer;
  transition: all 0.3s;
}

.customer-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.15);
}

.customer-card h3 {
  margin: 0 0 0.5rem;
  color: #2c3e50;
}

.fiscal-id {
  color: #7f8c8d;
  font-size: 0.9rem;
  margin: 0.5rem 0;
}

.address {
  color: #95a5a6;
  font-size: 0.85rem;
  margin: 0;
}

.loading, .error, .empty {
  padding: 2rem;
  text-align: center;
  background: white;
  border-radius: 8px;
}

.error {
  color: #e74c3c;
}
</style>
