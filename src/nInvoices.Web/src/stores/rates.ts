import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { ratesApi } from '@/api/rates'
import type { RateDto, CreateRateDto, UpdateRateDto } from '@/types'

export const useRatesStore = defineStore('rates', () => {
  const rates = ref<RateDto[]>([])
  const selectedRate = ref<RateDto | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const ratesByCustomer = computed(() => {
    return (customerId: number) => rates.value.filter(r => r.customerId === customerId)
  })

  const getRateById = computed(() => {
    return (id: number) => rates.value.find(r => r.id === id)
  })

  async function fetchAll() {
    try {
      loading.value = true
      error.value = null
      rates.value = await ratesApi.getAll()
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch rates'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function fetchById(id: number) {
    try {
      loading.value = true
      error.value = null
      selectedRate.value = await ratesApi.getById(id)
      return selectedRate.value
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch rate'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function fetchByCustomerId(customerId: number) {
    try {
      loading.value = true
      error.value = null
      const customerRates = await ratesApi.getByCustomerId(customerId)
      
      rates.value = [
        ...rates.value.filter(r => r.customerId !== customerId),
        ...customerRates
      ]
      
      return customerRates
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch customer rates'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function create(data: CreateRateDto) {
    try {
      loading.value = true
      error.value = null
      const newRate = await ratesApi.create(data)
      rates.value.push(newRate)
      return newRate
    } catch (err: any) {
      error.value = err.message || 'Failed to create rate'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function update(id: number, data: UpdateRateDto) {
    try {
      loading.value = true
      error.value = null
      const updatedRate = await ratesApi.update(id, data)

      const index = rates.value.findIndex(r => r.id === id)
      if (index !== -1) {
        rates.value[index] = updatedRate
      }
    } catch (err: any) {
      error.value = err.message || 'Failed to update rate'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function remove(id: number) {
    try {
      loading.value = true
      error.value = null
      await ratesApi.delete(id)
      rates.value = rates.value.filter(r => r.id !== id)
    } catch (err: any) {
      error.value = err.message || 'Failed to delete rate'
      throw err
    } finally {
      loading.value = false
    }
  }

  return {
    rates,
    selectedRate,
    loading,
    error,
    ratesByCustomer,
    getRateById,
    fetchAll,
    fetchById,
    fetchByCustomerId,
    create,
    update,
    remove
  }
})