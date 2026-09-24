<template>
  <aside class="variables" aria-label="Template variables">
    <div class="head">
      <span class="title">Insert</span>
      <input
        id="template-variable-search"
        v-model="query"
        type="search"
        class="search"
        placeholder="Search variables…"
        aria-label="Search variables"
      />
    </div>
    <div class="groups">
      <section v-for="group in filtered" :key="group.title" class="group">
        <h4>{{ group.title }}</h4>
        <button
          v-for="item in group.items"
          :key="item.label"
          type="button"
          class="item"
          :title="`Insert ${item.insert.replace('$0', '')}`"
          @click="emit('insert', item.insert)"
        >
          <code>{{ item.label }}</code>
          <span>{{ item.description }}</span>
        </button>
      </section>
      <p v-if="filtered.length === 0" class="none">No variable matches “{{ query }}”.</p>
    </div>
    <p class="hint">Type <code>[[</code> in the editor for suggestions.</p>
  </aside>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { VariableGroup } from './templateVariables'

const props = defineProps<{ groups: VariableGroup[] }>()
const emit = defineEmits<{ (e: 'insert', text: string): void }>()

const query = ref('')

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase()
  if (!q) return props.groups
  return props.groups
    .map(g => ({
      ...g,
      items: g.items.filter(i => i.label.toLowerCase().includes(q) || i.description.toLowerCase().includes(q))
    }))
    .filter(g => g.items.length > 0)
})
</script>

<style scoped>
.variables {
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
  background: var(--color-surface);
}

.head {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid var(--color-border);
}

.title {
  font-size: var(--text-sm);
  font-weight: 600;
}

.search {
  width: 100%;
  padding: 0.35rem 0.55rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  font-size: var(--text-sm);
}

.search:focus {
  outline: none;
  border-color: var(--color-primary);
  box-shadow: var(--focus-ring);
}

.groups {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 0.35rem 0.4rem 0.75rem;
}

.group h4 {
  margin: 0.75rem 0.35rem 0.25rem;
  font-size: var(--text-xs);
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.item {
  width: 100%;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.05rem;
  padding: 0.35rem 0.45rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.item:hover {
  background: var(--color-primary-soft);
}

.item code {
  font-size: 0.75rem;
  color: #7a2fb5;
  overflow-wrap: anywhere;
}

.item span {
  font-size: 0.72rem;
  line-height: 1.35;
  color: var(--color-text-muted);
}

.none {
  margin: 1rem 0.5rem;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.hint {
  margin: 0;
  padding: 0.5rem 0.75rem;
  border-top: 1px solid var(--color-border);
  font-size: 0.72rem;
  color: var(--color-text-muted);
}
</style>
