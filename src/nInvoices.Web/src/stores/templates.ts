import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { templatesApi } from '@/api/templates'
import type { 
  InvoiceTemplateDto, 
  CreateInvoiceTemplateDto, 
  UpdateInvoiceTemplateDto,
  TemplateValidationResultDto 
} from '@/types'

export const useTemplatesStore = defineStore('templates', () => {
  const templates = ref<InvoiceTemplateDto[]>([])
  const selectedTemplate = ref<InvoiceTemplateDto | null>(null)
  const validationResult = ref<TemplateValidationResultDto | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const templatesByCustomer = computed(() => {
    return (customerId: number) => templates.value.filter(t => t.customerId === customerId)
  })

  const getTemplateById = computed(() => {
    return (id: number) => templates.value.find(t => t.id === id)
  })

  const getTemplateByCustomerAndType = computed(() => {
    return (customerId: number, invoiceType: string) => 
      templates.value.find(t => t.customerId === customerId && t.invoiceType === invoiceType)
  })

  async function fetchAll() {
    try {
      loading.value = true
      error.value = null
      templates.value = await templatesApi.getAll()
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch templates'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function fetchById(id: number) {
    try {
      loading.value = true
      error.value = null
      selectedTemplate.value = await templatesApi.getById(id)
      return selectedTemplate.value
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch template'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function fetchByCustomerId(customerId: number) {
    try {
      loading.value = true
      error.value = null
      const customerTemplates = await templatesApi.getByCustomerId(customerId)
      
      templates.value = [
        ...templates.value.filter(t => t.customerId !== customerId),
        ...customerTemplates
      ]
      
      return customerTemplates
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch customer templates'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function create(data: CreateInvoiceTemplateDto) {
    try {
      loading.value = true
      error.value = null
      const newTemplate = await templatesApi.create(data)
      templates.value.push(newTemplate)
      return newTemplate
    } catch (err: any) {
      error.value = err.message || 'Failed to create template'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function update(id: number, data: UpdateInvoiceTemplateDto) {
    try {
      loading.value = true
      error.value = null
      await templatesApi.update(id, data)
      
      const index = templates.value.findIndex(t => t.id === id)
      if (index !== -1) {
        templates.value[index] = { ...templates.value[index], ...data }
      }
    } catch (err: any) {
      error.value = err.message || 'Failed to update template'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function remove(id: number) {
    try {
      loading.value = true
      error.value = null
      await templatesApi.delete(id)
      templates.value = templates.value.filter(t => t.id !== id)
    } catch (err: any) {
      error.value = err.message || 'Failed to delete template'
      throw err
    } finally {
      loading.value = false
    }
  }

  async function validateTemplate(content: string) {
    try {
      loading.value = true
      error.value = null
      validationResult.value = await templatesApi.validate(content)
      return validationResult.value
    } catch (err: any) {
      error.value = err.message || 'Failed to validate template'
      throw err
    } finally {
      loading.value = false
    }
  }

  return {
    templates,
    selectedTemplate,
    validationResult,
    loading,
    error,
    templatesByCustomer,
    getTemplateById,
    getTemplateByCustomerAndType,
    fetchAll,
    fetchById,
    fetchByCustomerId,
    create,
    update,
    remove,
    validateTemplate
  }
})