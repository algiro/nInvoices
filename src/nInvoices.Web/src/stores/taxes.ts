import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { taxesApi } from '@/api/taxes'
import type { TaxDto, CreateTaxDto, UpdateTaxDto } from '@/types'
import { errorMessage } from '@/composables/useToast'

export const useTaxesStore = defineStore('taxes', () => {
  const taxes = ref<TaxDto[]>([])
  const selectedTax = ref<TaxDto | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const taxesByCustomer = computed(() => {
    return (customerId: number) => taxes.value.filter(t => t.customerId === customerId)
  })

  const getTaxById = computed(() => {
    return (id: number) => taxes.value.find(t => t.id === id)
  })

  async function fetchAll() {
    try {
      loading.value = true
      error.value = null
      taxes.value = await taxesApi.getAll()
    } catch (err) {
      error.value = errorMessage(err, 'Failed to fetch taxes')
      throw err
    } finally {
      loading.value = false
    }
  }

  async function fetchById(id: number) {
    try {
      loading.value = true
      error.value = null
      selectedTax.value = await taxesApi.getById(id)
      return selectedTax.value
    } catch (err) {
      error.value = errorMessage(err, 'Failed to fetch tax')
      throw err
    } finally {
      loading.value = false
    }
  }

  async function fetchByCustomerId(customerId: number) {
    try {
      loading.value = true
      error.value = null
      const customerTaxes = await taxesApi.getByCustomerId(customerId)
      
      taxes.value = [
        ...taxes.value.filter(t => t.customerId !== customerId),
        ...customerTaxes
      ]
      
      return customerTaxes
    } catch (err) {
      error.value = errorMessage(err, 'Failed to fetch customer taxes')
      throw err
    } finally {
      loading.value = false
    }
  }

  async function create(data: CreateTaxDto) {
    try {
      loading.value = true
      error.value = null
      const newTax = await taxesApi.create(data)
      taxes.value.push(newTax)
      return newTax
    } catch (err) {
      error.value = errorMessage(err, 'Failed to create tax')
      throw err
    } finally {
      loading.value = false
    }
  }

  async function update(id: number, data: UpdateTaxDto) {
    try {
      loading.value = true
      error.value = null
      await taxesApi.update(id, data)
      
      const index = taxes.value.findIndex(t => t.id === id)
      if (index !== -1) {
        taxes.value[index] = { ...taxes.value[index], ...data }
      }
    } catch (err) {
      error.value = errorMessage(err, 'Failed to update tax')
      throw err
    } finally {
      loading.value = false
    }
  }

  async function remove(id: number) {
    try {
      loading.value = true
      error.value = null
      await taxesApi.delete(id)
      taxes.value = taxes.value.filter(t => t.id !== id)
    } catch (err) {
      error.value = errorMessage(err, 'Failed to delete tax')
      throw err
    } finally {
      loading.value = false
    }
  }

  return {
    taxes,
    selectedTax,
    loading,
    error,
    taxesByCustomer,
    getTaxById,
    fetchAll,
    fetchById,
    fetchByCustomerId,
    create,
    update,
    remove
  }
})