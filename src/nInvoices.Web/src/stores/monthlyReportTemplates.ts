import { defineStore } from 'pinia';
import { ref } from 'vue';
import { monthlyReportTemplatesApi } from '@/api/monthlyReportTemplates';
import type {
  MonthlyReportTemplateDto,
  CreateMonthlyReportTemplateDto,
  UpdateMonthlyReportTemplateDto,
  TemplateValidationResultDto
} from '@/types';
import { errorMessage } from '@/composables/useToast';

export const useMonthlyReportTemplatesStore = defineStore('monthlyReportTemplates', () => {
  const templates = ref<MonthlyReportTemplateDto[]>([]);
  const currentTemplate = ref<MonthlyReportTemplateDto | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  async function fetchByCustomer(customerId: number) {
    loading.value = true;
    error.value = null;
    try {
      // The customer's own templates and the shared ones, which apply when it has no active one
      const [own, shared] = await Promise.all([
        monthlyReportTemplatesApi.getByCustomer(customerId),
        monthlyReportTemplatesApi.getShared()
      ]);
      templates.value = [...own, ...shared];
    } catch (err) {
      error.value = errorMessage(err, 'Failed to fetch templates');
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function fetchById(id: number) {
    loading.value = true;
    error.value = null;
    try {
      currentTemplate.value = await monthlyReportTemplatesApi.getById(id);
      return currentTemplate.value;
    } catch (err) {
      error.value = errorMessage(err, 'Failed to fetch template');
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function create(dto: CreateMonthlyReportTemplateDto) {
    loading.value = true;
    error.value = null;
    try {
      const created = await monthlyReportTemplatesApi.create(dto);
      templates.value.push(created);
      return created;
    } catch (err) {
      error.value = errorMessage(err, 'Failed to create template');
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function update(id: number, dto: UpdateMonthlyReportTemplateDto) {
    loading.value = true;
    error.value = null;
    try {
      const updated = await monthlyReportTemplatesApi.update(id, dto);
      const index = templates.value.findIndex(t => t.id === id);
      if (index !== -1) {
        templates.value[index] = updated;
      }
      if (currentTemplate.value?.id === id) {
        currentTemplate.value = updated;
      }
      return updated;
    } catch (err) {
      error.value = errorMessage(err, 'Failed to update template');
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function deleteTemplate(id: number) {
    loading.value = true;
    error.value = null;
    try {
      await monthlyReportTemplatesApi.delete(id);
      templates.value = templates.value.filter(t => t.id !== id);
      if (currentTemplate.value?.id === id) {
        currentTemplate.value = null;
      }
    } catch (err) {
      error.value = errorMessage(err, 'Failed to delete template');
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function activate(id: number) {
    loading.value = true;
    error.value = null;
    try {
      await monthlyReportTemplatesApi.activate(id);
      // Deactivate other templates for same customer and invoice type
      const template = templates.value.find(t => t.id === id);
      if (template) {
        templates.value = templates.value.map(t => ({
          ...t,
          isActive: t.customerId === template.customerId && 
                    t.invoiceType === template.invoiceType ? 
                    t.id === id : t.isActive
        }));
      }
    } catch (err) {
      error.value = errorMessage(err, 'Failed to activate template');
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function deactivate(id: number) {
    loading.value = true;
    error.value = null;
    try {
      await monthlyReportTemplatesApi.deactivate(id);
      const template = templates.value.find(t => t.id === id);
      if (template) {
        template.isActive = false;
      }
    } catch (err) {
      error.value = errorMessage(err, 'Failed to deactivate template');
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function validate(content: string): Promise<TemplateValidationResultDto> {
    loading.value = true;
    error.value = null;
    try {
      return await monthlyReportTemplatesApi.validate(content);
    } catch (err) {
      error.value = errorMessage(err, 'Failed to validate template');
      throw err;
    } finally {
      loading.value = false;
    }
  }

  function clearError() {
    error.value = null;
  }

  return {
    templates,
    currentTemplate,
    loading,
    error,
    fetchByCustomer,
    fetchById,
    create,
    update,
    deleteTemplate,
    activate,
    deactivate,
    validate,
    clearError
  };
});
