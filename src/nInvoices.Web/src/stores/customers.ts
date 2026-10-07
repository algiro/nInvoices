import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { customersApi } from '../api';
import type { CustomerDto, CreateCustomerDto, UpdateCustomerDto } from '../types';
import { errorMessage } from '@/composables/useToast';

/**
 * Customers Store
 * Manages customer state using Pinia with Composition API
 * Follows Single Responsibility Principle
 */
export const useCustomersStore = defineStore('customers', () => {
  // State
  const customers = ref<CustomerDto[]>([]);
  const selectedCustomer = ref<CustomerDto | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  // Getters
  const customerCount = computed(() => customers.value.length);
  
  const getCustomerById = computed(() => {
    return (id: number) => customers.value.find(c => c.id === id);
  });

  // Actions
  async function fetchAll() {
    loading.value = true;
    error.value = null;
    try {
      customers.value = await customersApi.getAll();
    } catch (e) {
      error.value = errorMessage(e, 'Failed to fetch customers');
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function fetchById(id: number) {
    loading.value = true;
    error.value = null;
    try {
      selectedCustomer.value = await customersApi.getById(id);
      return selectedCustomer.value;
    } catch (e) {
      error.value = errorMessage(e, 'Failed to fetch customer');
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function create(data: CreateCustomerDto) {
    loading.value = true;
    error.value = null;
    try {
      const newCustomer = await customersApi.create(data);
      customers.value.push(newCustomer);
      return newCustomer;
    } catch (e) {
      error.value = errorMessage(e, 'Failed to create customer');
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function update(id: number, data: UpdateCustomerDto) {
    loading.value = true;
    error.value = null;
    try {
      await customersApi.update(id, data);
      const index = customers.value.findIndex(c => c.id === id);
      if (index !== -1) {
        await fetchById(id); // Refresh the customer data
      }
    } catch (e) {
      error.value = errorMessage(e, 'Failed to update customer');
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function remove(id: number) {
    loading.value = true;
    error.value = null;
    try {
      await customersApi.delete(id);
      customers.value = customers.value.filter(c => c.id !== id);
      if (selectedCustomer.value?.id === id) {
        selectedCustomer.value = null;
      }
    } catch (e) {
      error.value = errorMessage(e, 'Failed to delete customer');
      throw e;
    } finally {
      loading.value = false;
    }
  }

  function clearError() {
    error.value = null;
  }

  return {
    // State
    customers,
    selectedCustomer,
    loading,
    error,
    // Getters
    customerCount,
    getCustomerById,
    // Actions
    fetchAll,
    fetchById,
    create,
    update,
    remove,
    clearError,
  };
});
