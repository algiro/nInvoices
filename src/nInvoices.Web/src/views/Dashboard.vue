<script setup lang="ts">
import { onMounted, computed } from 'vue';
import { useCustomersStore } from '../stores/customers';
import { useInvoicesStore } from '../stores/invoices';

const customersStore = useCustomersStore();
const invoicesStore = useInvoicesStore();

onMounted(async () => {
  await Promise.all([
    customersStore.fetchAll(),
    invoicesStore.fetchAll()
  ]);
});

const stats = computed(() => ({
  customers: customersStore.customerCount,
  invoices: invoicesStore.invoiceCount,
  drafts: invoicesStore.draftInvoices.length,
  revenue: invoicesStore.totalRevenue,
}));
</script>

<template>
  <div class="dashboard">
    <h1>Dashboard</h1>

    <div class="stats-grid">
      <div class="stat-card">
        <div class="stat-icon">👥</div>
        <div class="stat-content">
          <h3>{{ stats.customers }}</h3>
          <p>Total Customers</p>
        </div>
      </div>

      <div class="stat-card">
        <div class="stat-icon">📄</div>
        <div class="stat-content">
          <h3>{{ stats.invoices }}</h3>
          <p>Total Invoices</p>
        </div>
      </div>

      <div class="stat-card">
        <div class="stat-icon">📝</div>
        <div class="stat-content">
          <h3>{{ stats.drafts }}</h3>
          <p>Draft Invoices</p>
        </div>
      </div>

      <div class="stat-card">
        <div class="stat-icon">💰</div>
        <div class="stat-content">
          <h3>{{ stats.revenue.toFixed(2) }}</h3>
          <p>Total Revenue</p>
        </div>
      </div>
    </div>

    <div class="quick-actions">
      <h2>Quick Actions</h2>
      <div class="actions-grid">
        <router-link to="/customers/new" class="action-btn">
          <span>👤</span> New Customer
        </router-link>
        <router-link to="/invoices/new" class="action-btn">
          <span>📄</span> Generate Invoice
        </router-link>
      </div>
    </div>
  </div>
</template>

<style scoped>
.dashboard {
  max-width: 1200px;
}

h1 {
  margin-bottom: 2rem;
  color: #2c3e50;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1.5rem;
  margin-bottom: 3rem;
}

.stat-card {
  background: white;
  padding: 1.5rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  display: flex;
  align-items: center;
  gap: 1rem;
}

.stat-icon {
  font-size: 3rem;
}

.stat-content h3 {
  margin: 0;
  font-size: 2rem;
  color: #3498db;
}

.stat-content p {
  margin: 0.5rem 0 0;
  color: #7f8c8d;
  font-size: 0.9rem;
}

.quick-actions {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.quick-actions h2 {
  margin-top: 0;
  color: #2c3e50;
}

.actions-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.action-btn {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 1rem 1.5rem;
  background: #3498db;
  color: white;
  text-decoration: none;
  border-radius: 6px;
  font-weight: 500;
  transition: all 0.3s;
}

.action-btn:hover {
  background: #2980b9;
  transform: translateY(-2px);
  box-shadow: 0 4px 8px rgba(0,0,0,0.2);
}

.action-btn span {
  font-size: 1.5rem;
}
</style>
