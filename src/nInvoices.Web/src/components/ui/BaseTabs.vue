<template>
  <div class="tabs" role="tablist" :aria-label="label">
    <button
      v-for="tab in tabs"
      :id="`tab-${tab.id}`"
      :key="tab.id"
      type="button"
      role="tab"
      class="tab"
      :class="{ active: modelValue === tab.id }"
      :aria-selected="modelValue === tab.id"
      :tabindex="modelValue === tab.id ? 0 : -1"
      @click="emit('update:modelValue', tab.id)"
      @keydown="onKeydown($event, tab.id)"
    >
      {{ tab.label }}
      <span v-if="tab.count !== undefined" class="count">{{ tab.count }}</span>
    </button>
  </div>
</template>

<script setup lang="ts">
export interface TabItem {
  id: string
  label: string
  count?: number
}

const props = defineProps<{
  tabs: TabItem[]
  modelValue: string
  label?: string
}>()

const emit = defineEmits<{ (e: 'update:modelValue', id: string): void }>()

// Arrow keys move between tabs, as screen-reader users expect from a tablist
function onKeydown(event: KeyboardEvent, id: string) {
  if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return
  const index = props.tabs.findIndex(t => t.id === id)
  const next = props.tabs[(index + (event.key === 'ArrowRight' ? 1 : -1) + props.tabs.length) % props.tabs.length]
  emit('update:modelValue', next.id)
  requestAnimationFrame(() => document.getElementById(`tab-${next.id}`)?.focus())
  event.preventDefault()
}
</script>

<style scoped>
.tabs {
  display: flex;
  gap: 0.25rem;
  border-bottom: 1px solid var(--color-border);
  overflow-x: auto;
  scrollbar-width: none;
}

.tab {
  position: relative;
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.6rem 0.75rem;
  border: 0;
  border-radius: 0;
  background: transparent;
  color: var(--color-text-muted);
  font-size: var(--text-md);
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
}

.tab:hover {
  color: var(--color-text);
}

.tab.active {
  color: var(--color-primary);
  font-weight: 600;
}

.tab.active::after {
  content: '';
  position: absolute;
  left: 0.5rem;
  right: 0.5rem;
  bottom: -1px;
  height: 2px;
  border-radius: 2px;
  background: var(--color-primary);
}

.count {
  min-width: 1.25rem;
  padding: 0 0.35rem;
  border-radius: 999px;
  background: var(--color-surface-sunken);
  color: var(--color-text-muted);
  font-size: var(--text-xs);
  font-weight: 600;
  text-align: center;
}

.tab.active .count {
  background: var(--color-primary-soft);
  color: var(--color-primary);
}
</style>
