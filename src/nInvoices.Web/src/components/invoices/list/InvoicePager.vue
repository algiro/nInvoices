<template>
  <nav class="pager" aria-label="Pages">
    <span class="muted">{{ rangeLabel }}</span>
    <div class="pager-controls">
      <label class="page-size">
        Rows
        <select class="control" :value="params.pageSize" @change="setPageSize(selectValue($event))">
          <option v-for="size in PAGE_SIZES" :key="size" :value="size">{{ size }}</option>
        </select>
      </label>
      <BaseButton size="sm" icon="arrowLeft" icon-only aria-label="Previous page" :disabled="page.page <= 1" @click="goToPage(page.page - 1)" />
      <template v-for="item in pageItems" :key="item.key">
        <span v-if="item.gap" class="gap" aria-hidden="true">…</span>
        <button
          v-else
          type="button"
          class="page-button"
          :class="{ active: item.page === page.page }"
          :aria-current="item.page === page.page ? 'page' : undefined"
          @click="goToPage(item.page!)"
        >
          {{ item.page }}
        </button>
      </template>
      <BaseButton size="sm" icon="chevronRight" icon-only aria-label="Next page" :disabled="page.page >= lastPage" @click="goToPage(page.page + 1)" />
    </div>
  </nav>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { InvoicePageDto } from '@/types'
import BaseButton from '@/components/ui/BaseButton.vue'
import { useInvoiceListQuery, selectValue, PAGE_SIZES, DEFAULT_PAGE_SIZE } from '@/composables/useInvoiceListQuery'

const props = defineProps<{ page: InvoicePageDto }>()

const { params, setQuery } = useInvoiceListQuery()

const lastPage = computed(() => Math.max(1, Math.ceil(props.page.totalCount / params.value.pageSize)))

const rangeLabel = computed(() => {
  if (props.page.totalCount === 0) return ''
  const first = (props.page.page - 1) * props.page.pageSize + 1
  const last = first + props.page.items.length - 1
  return `${first}–${last} of ${props.page.totalCount}`
})

/** Page numbers around the current one, with the first and last always shown. */
const pageItems = computed(() => {
  const current = props.page.page
  const pages = new Set([1, lastPage.value, current - 1, current, current + 1].filter(p => p >= 1 && p <= lastPage.value))
  const sorted = [...pages].sort((a, b) => a - b)
  const items: { key: string; page?: number; gap?: boolean }[] = []
  sorted.forEach((p, i) => {
    if (i > 0 && p - sorted[i - 1] > 1) items.push({ key: `gap-${p}`, gap: true })
    items.push({ key: `p-${p}`, page: p })
  })
  return items
})

function goToPage(target: number) {
  setQuery({ page: target === 1 ? undefined : target })
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

function setPageSize(size: number | string) {
  setQuery({ size: size === DEFAULT_PAGE_SIZE ? undefined : size })
}
</script>

<style scoped>
.pager {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  margin-top: 0.75rem;
  font-size: var(--text-sm);
}

.pager-controls {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.3rem;
}

.page-size {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  margin-right: 0.5rem;
  color: var(--color-text-muted);
}

.page-size .control {
  width: auto;
}

.page-button {
  min-width: 2rem;
  padding: 0.3rem 0.5rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
  cursor: pointer;
}

.page-button:hover {
  border-color: var(--color-border-strong);
  color: var(--color-text);
}

.page-button.active {
  background: var(--color-primary);
  border-color: var(--color-primary);
  color: var(--color-on-primary);
  font-weight: 600;
}

.gap {
  padding: 0 0.2rem;
  color: var(--color-text-subtle);
}
</style>
