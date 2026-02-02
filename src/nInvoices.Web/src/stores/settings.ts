import { defineStore } from 'pinia'
import { ref } from 'vue'
import axios from 'axios'

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5297'

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
      const response = await axios.get<InvoiceSettings>(`${API_URL}/api/settings/invoice`)
      invoiceSettings.value = response.data
      return response.data
    } catch (err: any) {
      error.value = err.message || 'Failed to load invoice settings'
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
