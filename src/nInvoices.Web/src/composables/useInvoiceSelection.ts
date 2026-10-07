import { ref, reactive, computed, watch, type Ref } from 'vue'
import { invoicesApi } from '@/api/invoices'
import { useToast } from '@/composables/useToast'
import type { InvoiceDto, InvoicePageDto } from '@/types'
import type { InvoiceListParams } from '@/composables/useInvoiceListQuery'

const MAX_SELECT_ALL = 500

/**
 * The invoices ticked in the list. Kept across pages and sort orders; a different filter starts
 * a new selection.
 */
export function useInvoiceSelection(page: Ref<InvoicePageDto | null>, params: Ref<InvoiceListParams>) {
  const toast = useToast()
  const selected = reactive(new Map<number, InvoiceDto>())

  watch(() => JSON.stringify([params.value.status, params.value.customerId, params.value.type, params.value.year, params.value.search]), () => selected.clear())

  const pageAllSelected = computed(() => !!page.value?.items.length && page.value.items.every(i => selected.has(i.id)))
  const pageSomeSelected = computed(() => !!page.value?.items.some(i => selected.has(i.id)))

  function toggle(invoice: InvoiceDto) {
    if (selected.has(invoice.id)) selected.delete(invoice.id)
    else selected.set(invoice.id, invoice)
  }

  function togglePage() {
    const items = page.value?.items ?? []
    if (pageAllSelected.value) items.forEach(i => selected.delete(i.id))
    else items.forEach(i => selected.set(i.id, i))
  }

  const selectingAll = ref(false)
  const canSelectAllMatching = computed(() =>
    pageAllSelected.value && !!page.value && page.value.totalCount > selected.size && page.value.totalCount <= MAX_SELECT_ALL)

  /** Every invoice matching the filters, fetched page by page. */
  async function selectAllMatching() {
    selectingAll.value = true
    try {
      const pageSize = 200
      for (let p = 1; p <= Math.ceil((page.value?.totalCount ?? 0) / pageSize); p++) {
        const result = await invoicesApi.search({ ...params.value, page: p, pageSize })
        result.items.forEach(i => selected.set(i.id, i))
      }
    } catch (error) {
      toast.failure('Could not select all the invoices', error)
    } finally {
      selectingAll.value = false
    }
  }

  return { selected, pageAllSelected, pageSomeSelected, toggle, togglePage, selectingAll, canSelectAllMatching, selectAllMatching }
}

export type InvoiceSelection = ReturnType<typeof useInvoiceSelection>
