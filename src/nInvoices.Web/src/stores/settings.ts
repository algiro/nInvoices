import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiClient } from '../api/client'
import { errorMessage } from '@/composables/useToast'

export interface InvoiceSettings {
  numberFormat: string
  firstDayOfWeek: number
}

export const useSettingsStore = defineStore('settings', () => {
  const invoiceSettings = ref<InvoiceSettings | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchInvoiceSettings() {
    loading.value = true
    error.value = null
    try {
      const data = await apiClient.get<InvoiceSettings>('/api/settings/invoice')
      invoiceSettings.value = data
      return data
    } catch (err) {
      error.value = errorMessage(err, 'Failed to load invoice settings')
      throw err
    } finally {
      loading.value = false
    }
  }

  function getFirstDayOfWeek(): number {
    return invoiceSettings.value?.firstDayOfWeek ?? 1 // Default to Monday
  }

  return {
    invoiceSettings,
    loading,
    error,
    fetchInvoiceSettings,
    getFirstDayOfWeek
  }
})
