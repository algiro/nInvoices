import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { invoicesApi } from '../api';
import type { InvoiceDto, GenerateInvoiceDto, UpdateInvoiceDto } from '../types';

/**
 * Invoices Store
 * Manages invoice state with reactive updates
 */
export const useInvoicesStore = defineStore('invoices', () => {
  // State
  const invoices = ref<InvoiceDto[]>([]);
  const selectedInvoice = ref<InvoiceDto | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  // Getters
  const invoiceCount = computed(() => invoices.value.length);
  
  const draftInvoices = computed(() => 
    invoices.value.filter(i => i.status === 'Draft')
  );

  const finalizedInvoices = computed(() => 
    invoices.value.filter(i => i.status === 'Finalized')
  );

  const paidInvoices = computed(() => 
    invoices.value.filter(i => i.status === 'Paid')
  );

  const totalRevenue = computed(() => 
    invoices.value
      .filter(i => i.status === 'Paid')
      .reduce((sum, inv) => sum + inv.total.amount, 0)
  );

  // Actions
  async function fetchAll() {
    loading.value = true;
    error.value = null;
    try {
      invoices.value = await invoicesApi.getAll();
    } catch (e: any) {
      error.value = e.message || 'Failed to fetch invoices';
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function fetchById(id: number) {
    loading.value = true;
    error.value = null;
    try {
      selectedInvoice.value = await invoicesApi.getById(id);
      return selectedInvoice.value;
    } catch (e: any) {
      error.value = e.message || 'Failed to fetch invoice';
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function fetchByCustomer(customerId: number) {
    loading.value = true;
    error.value = null;
    try {
      invoices.value = await invoicesApi.getByCustomer(customerId);
    } catch (e: any) {
      error.value = e.message || 'Failed to fetch customer invoices';
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function generate(data: GenerateInvoiceDto) {
    loading.value = true;
    error.value = null;
    try {
      const newInvoice = await invoicesApi.generate(data);
      invoices.value.push(newInvoice);
      return newInvoice;
    } catch (e: any) {
      error.value = e.message || 'Failed to generate invoice';
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function update(id: number, data: UpdateInvoiceDto) {
    loading.value = true;
    error.value = null;
    try {
      await invoicesApi.update(id, data);
      await fetchById(id); // Refresh
    } catch (e: any) {
      error.value = e.message || 'Failed to update invoice';
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function finalize(id: number) {
    loading.value = true;
    error.value = null;
    try {
      await invoicesApi.finalize(id);
      await fetchById(id); // Refresh
    } catch (e: any) {
      error.value = e.message || 'Failed to finalize invoice';
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function remove(id: number) {
    loading.value = true;
    error.value = null;
    try {
      await invoicesApi.delete(id);
      invoices.value = invoices.value.filter(i => i.id !== id);
      if (selectedInvoice.value?.id === id) {
        selectedInvoice.value = null;
      }
    } catch (e: any) {
      error.value = e.message || 'Failed to delete invoice';
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function downloadPdf(id: number, invoiceNumber: string) {
    loading.value = true;
    error.value = null;
    try {
      const blob = await invoicesApi.downloadPdf(id);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `Invoice-${invoiceNumber}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (e: any) {
      error.value = e.message || 'Failed to download PDF';
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function downloadMonthlyReportPdf(id: number, invoiceNumber: string) {
    loading.value = true;
    error.value = null;
    try {
      const blob = await invoicesApi.downloadMonthlyReportPdf(id);
      
      // Check if the blob is actually JSON (error response)
      if (blob.type === 'application/json') {
        const text = await blob.text();
        const errorData = JSON.parse(text);
        throw new Error(errorData.error || 'Failed to download monthly report');
      }
      
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `MonthlyReport-${invoiceNumber}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (e: any) {
      error.value = e.message || 'Failed to download monthly report';
      console.error('Monthly report download error:', e);
      alert('Failed to download monthly report: ' + e.message);
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function regenerateInvoicePdf(id: number) {
    loading.value = true;
    error.value = null;
    try {
      const result = await invoicesApi.regenerateInvoicePdf(id);
      await fetchById(id); // Refresh invoice data
      return result;
    } catch (e: any) {
      error.value = e.message || 'Failed to regenerate invoice PDF';
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function regenerateMonthlyReportPdf(id: number) {
    loading.value = true;
    error.value = null;
    try {
      const result = await invoicesApi.regenerateMonthlyReportPdf(id);
      return result;
    } catch (e: any) {
      error.value = e.message || 'Failed to regenerate monthly report PDF';
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
    invoices,
    selectedInvoice,
    loading,
    error,
    // Getters
    invoiceCount,
    draftInvoices,
    finalizedInvoices,
    paidInvoices,
    totalRevenue,
    // Actions
    fetchAll,
    fetchById,
    fetchByCustomer,
    generate,
    update,
    finalize,
    remove,
    downloadPdf,
    downloadMonthlyReportPdf,
    regenerateInvoicePdf,
    regenerateMonthlyReportPdf,
    clearError,
  };
});
