import { computed } from 'vue'
import { useRoute, useRouter, type LocationQueryRaw } from 'vue-router'
import type { InvoiceSearchParams, InvoiceSortField } from '@/types'

export const PAGE_SIZES = [25, 50, 100]
export const DEFAULT_PAGE_SIZE = 25
const SORT_FIELDS: InvoiceSortField[] = ['IssueDate', 'Number', 'Customer', 'Period', 'Status', 'Total']

/** The URL keys a saved view keeps: every filter, the sort order and the page size, not the page. */
const FILTER_KEYS = ['status', 'customerId', 'type', 'year', 'q', 'sort', 'dir', 'size']
const NARROWING_KEYS = ['status', 'customerId', 'type', 'year', 'q']

export type InvoiceListParams = Required<Pick<InvoiceSearchParams, 'sort' | 'dir' | 'page' | 'pageSize'>> & InvoiceSearchParams

/**
 * The invoice list's state, kept in the query string so a view can be reloaded, linked to and
 * saved. Defaults are left out of it. Holds no state of its own: every caller reads the same URL.
 */
export function useInvoiceListQuery() {
  const route = useRoute()
  const router = useRouter()

  function queryValue(key: string): string {
    const value = route.query[key]
    return typeof value === 'string' ? value : ''
  }

  const params = computed((): InvoiceListParams => {
    const sort = queryValue('sort') as InvoiceSortField
    const size = Number(queryValue('size'))
    return {
      status: queryValue('status') || undefined,
      customerId: Number(queryValue('customerId')) || undefined,
      type: queryValue('type') || undefined,
      year: Number(queryValue('year')) || undefined,
      search: queryValue('q') || undefined,
      sort: SORT_FIELDS.includes(sort) ? sort : 'IssueDate',
      dir: queryValue('dir') === 'asc' ? 'asc' : 'desc',
      page: Math.max(1, Number(queryValue('page')) || 1),
      pageSize: PAGE_SIZES.includes(size) ? size : DEFAULT_PAGE_SIZE
    }
  })

  /** The filter part of the URL (no page), as saved in a view. */
  const filterQuery = computed(() =>
    Object.fromEntries(FILTER_KEYS.map(key => [key, queryValue(key)]).filter(([, value]) => value !== '')) as Record<string, string>
  )

  /** Whether anything narrows the list (sorting and page size don't). */
  const hasFilters = computed(() => NARROWING_KEYS.some(key => queryValue(key) !== ''))

  /** Changes query values ('' / 0 / undefined remove them); any change but the page goes back to page 1. */
  function setQuery(changes: Record<string, string | number | undefined>) {
    const next: LocationQueryRaw = { ...route.query }
    for (const [key, value] of Object.entries(changes)) {
      if (value === undefined || value === '' || value === 0) delete next[key]
      else next[key] = String(value)
    }
    if (!('page' in changes)) delete next.page
    router.replace({ query: next })
  }

  /** Replaces the whole query, e.g. with a saved view; `{}` clears every filter. */
  function applyView(query: Record<string, string>) {
    router.replace({ query })
  }

  return { queryValue, params, filterQuery, hasFilters, setQuery, applyView }
}

/** A <select>'s value, as a number when it is one. */
export function selectValue(event: Event): number | string {
  const value = (event.target as HTMLSelectElement).value
  return /^\d+$/.test(value) ? Number(value) : value
}
