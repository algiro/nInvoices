<template>
  <section class="panel" :class="{ flush }">
    <header v-if="title || $slots.header || $slots.actions" class="panel-header">
      <div class="panel-titles">
        <slot name="header">
          <h2 class="panel-title">{{ title }}</h2>
          <p v-if="description" class="panel-description">{{ description }}</p>
        </slot>
      </div>
      <div v-if="$slots.actions" class="panel-actions">
        <slot name="actions" />
      </div>
    </header>
    <div class="panel-body">
      <slot />
    </div>
    <footer v-if="$slots.footer" class="panel-footer">
      <slot name="footer" />
    </footer>
  </section>
</template>

<script setup lang="ts">
defineProps<{
  title?: string
  description?: string
  /** Body without padding, for tables and lists that run edge to edge. */
  flush?: boolean
}>()
</script>

<style scoped>
.panel {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  min-width: 0;
}

.panel-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  padding: 0.9rem 1.15rem;
  border-bottom: 1px solid var(--color-border);
}

.panel-title {
  font-size: 1rem;
  font-weight: 600;
}

.panel-description {
  margin: 0.2rem 0 0;
  max-width: 65ch;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.panel-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.panel-body {
  padding: 1.15rem;
}

.flush .panel-body {
  padding: 0;
}

/* tables inside a flush panel lose their own frame */
.flush .panel-body :deep(.table-wrap) {
  border: 0;
  border-radius: 0 0 var(--radius-lg) var(--radius-lg);
}

.panel-footer {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 0.5rem;
  padding: 0.8rem 1.15rem;
  border-top: 1px solid var(--color-border);
  background: var(--color-surface-muted);
  border-radius: 0 0 var(--radius-lg) var(--radius-lg);
}
</style>
