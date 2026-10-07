<template>
  <div class="filters">
    <div class="filters-top">
      <div class="status-chips" role="group" aria-label="Filter by status">
        <button
          v-for="chip in statusChips"
          :key="chip.value"
          type="button"
          class="chip"
          :class="{ active: (params.status ?? '') === chip.value }"
          :aria-pressed="(params.status ?? '') === chip.value"
          @click="setQuery({ status: chip.value })"
        >
          {{ chip.label }}
          <span class="chip-count">{{ chip.count }}</span>
        </button>
      </div>

      <div class="views">
        <select
          id="invoice-saved-view"
          class="control"
          aria-label="Saved views"
          :value="currentView?.name ?? ''"
          @change="onViewSelected(($event.target as HTMLSelectElement).value)"
        >
          <option value="">{{ savedViews.views.value.length ? 'Saved views…' : 'No saved views' }}</option>
          <option v-for="view in savedViews.views.value" :key="view.name" :value="view.name">{{ view.name }}</option>
        </select>
        <BaseButton
          v-if="currentView"
          size="sm"
          variant="ghost-danger"
          icon="trash"
          icon-only
          :aria-label="`Delete the view ${currentView.name}`"
          title="Delete this view"
          @click="deleteView(currentView.name)"
        />
        <BaseButton v-else size="sm" variant="ghost" icon="plus" :disabled="!hasFilters" @click="openSaveView">Save view</BaseButton>
      </div>
    </div>

    <div class="filter-controls">
      <div class="search">
        <AppIcon name="search" class="search-icon" />
        <input
          id="invoice-search"
          v-model="searchText"
          type="search"
          class="control"
          placeholder="Invoice number or customer"
          aria-label="Search invoices"
        />
      </div>
      <select id="invoice-customer-filter" class="control" aria-label="Filter by customer" :value="params.customerId ?? 0" @change="setQuery({ customerId: selectValue($event) })">
        <option :value="0">All customers</option>
        <option v-for="c in sortedCustomers" :key="c.id" :value="c.id">{{ c.name }}</option>
      </select>
      <select id="invoice-type-filter" class="control" aria-label="Filter by type" :value="params.type ?? ''" @change="setQuery({ type: selectValue($event) })">
        <option value="">All types</option>
        <option value="Monthly">Monthly</option>
        <option value="OneTime">One-time</option>
      </select>
      <select id="invoice-year-filter" class="control" aria-label="Filter by year issued" :value="params.year ?? 0" @change="setQuery({ year: selectValue($event) })">
        <option :value="0">All years</option>
        <option v-for="year in years" :key="year" :value="year">{{ year }}</option>
      </select>
      <BaseButton v-if="hasFilters" variant="ghost" size="sm" @click="applyView({})">Clear filters</BaseButton>
    </div>
  </div>

  <BaseDialog :open="saveViewOpen" title="Save this view" description="Filters, sort order and rows per page, under a name. Saved in this browser." size="sm" @close="saveViewOpen = false">
    <form id="save-view-form" novalidate @submit.prevent="confirmSaveView">
      <BaseField label="Name" for="view-name" required>
        <input id="view-name" v-model="viewName" type="text" class="control" placeholder="e.g. Unpaid this year" maxlength="60" />
      </BaseField>
    </form>
    <template #footer>
      <BaseButton @click="saveViewOpen = false">Cancel</BaseButton>
      <BaseButton type="submit" form="save-view-form" variant="primary" :disabled="!viewName.trim()">Save view</BaseButton>
    </template>
  </BaseDialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useCustomersStore } from '@/stores/customers'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import BaseField from '@/components/ui/BaseField.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import { useInvoiceListQuery, selectValue } from '@/composables/useInvoiceListQuery'
import { useSavedViews } from '@/composables/useSavedViews'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import { invoiceStatus } from '@/utils/format'

const props = defineProps<{
  /** Invoices per status among those matching the other filters. */
  statusCounts: Record<string, number>
  /** The years invoices were issued in. */
  years: number[]
}>()

const customersStore = useCustomersStore()
const toast = useToast()
const { confirm } = useConfirm()
const { queryValue, params, filterQuery, hasFilters, setQuery, applyView } = useInvoiceListQuery()
const savedViews = useSavedViews('ninvoices.invoiceViews')

const sortedCustomers = computed(() => [...customersStore.customers].sort((a, b) => a.name.localeCompare(b.name)))

const statusChips = computed(() => {
  const counts = props.statusCounts
  const all = Object.values(counts).reduce((sum, n) => sum + n, 0)
  return [
    { value: '', label: 'All', count: all },
    ...['Draft', 'Finalized', 'Sent', 'Paid', 'Cancelled'].map(name => ({
      value: name,
      label: invoiceStatus(name).label,
      count: counts[name] ?? 0
    }))
  ]
})

// ---------- search box: typed text reaches the URL after a pause ----------

const searchText = ref(queryValue('q'))
let searchTimer: ReturnType<typeof setTimeout> | undefined

watch(searchText, text => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    if (text.trim() !== queryValue('q')) setQuery({ q: text.trim() })
  }, 300)
})

// The URL changed under the box (filters cleared, a view applied): show it, and drop a pending search
watch(() => queryValue('q'), value => {
  if (value !== searchText.value.trim()) {
    clearTimeout(searchTimer)
    searchText.value = value
  }
})

// ---------- saved views ----------

const currentView = computed(() => savedViews.find(filterQuery.value))
const saveViewOpen = ref(false)
const viewName = ref('')

function onViewSelected(name: string) {
  const view = savedViews.views.value.find(v => v.name === name)
  if (view) applyView(view.query)
}

function openSaveView() {
  viewName.value = ''
  saveViewOpen.value = true
  requestAnimationFrame(() => document.getElementById('view-name')?.focus())
}

function confirmSaveView() {
  if (!viewName.value.trim()) return
  savedViews.save(viewName.value, filterQuery.value)
  saveViewOpen.value = false
  toast.success(`View “${viewName.value.trim()}” saved`)
}

async function deleteView(name: string) {
  if (!(await confirm({ title: `Delete the view “${name}”?`, message: 'The invoices are not affected.', confirmLabel: 'Delete', tone: 'danger' }))) return
  savedViews.remove(name)
}
</script>

<style scoped>
.filters {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.filters-top {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
}

.status-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.chip {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.3rem 0.7rem;
  border: 1px solid var(--color-border-strong);
  border-radius: 999px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
  font-weight: 500;
}

.chip:hover {
  border-color: var(--color-primary-line);
  color: var(--color-primary);
}

.chip.active {
  background: var(--color-primary);
  border-color: var(--color-primary);
  color: var(--color-on-primary);
}

.chip-count {
  font-size: var(--text-xs);
  font-weight: 600;
  opacity: 0.75;
  font-variant-numeric: tabular-nums;
}

.views {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.views .control {
  width: auto;
  min-width: 11rem;
}

.filter-controls {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.filter-controls > .control {
  width: auto;
  min-width: 9rem;
}

.search {
  position: relative;
  flex: 1 1 18rem;
  max-width: 26rem;
}

.search-icon {
  position: absolute;
  left: 0.7rem;
  top: 50%;
  width: 1rem;
  height: 1rem;
  transform: translateY(-50%);
  color: var(--color-text-subtle);
  pointer-events: none;
}

.search .control {
  padding-left: 2.1rem;
}
</style>
