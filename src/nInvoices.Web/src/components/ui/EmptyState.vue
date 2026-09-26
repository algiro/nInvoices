<template>
  <div class="empty-state" :class="{ compact }">
    <AppIcon v-if="icon" :name="icon" class="icon" />
    <strong>{{ title }}</strong>
    <p v-if="description">{{ description }}</p>
    <div v-if="$slots.default" class="actions">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
import AppIcon, { type IconName } from './AppIcon.vue'

defineProps<{
  title: string
  description?: string
  icon?: IconName
  compact?: boolean
}>()
</script>

<style scoped>
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.4rem;
  padding: 3rem 1.5rem;
  border: 1px dashed var(--color-border-strong);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  text-align: center;
  color: var(--color-text-muted);
  font-size: var(--text-md);
}

.empty-state.compact {
  padding: 1.75rem 1rem;
}

.icon {
  width: 2rem;
  height: 2rem;
  margin-bottom: 0.25rem;
  color: var(--color-text-subtle);
}

strong {
  color: var(--color-text);
  font-size: var(--text-base);
}

p {
  margin: 0;
  max-width: 46ch;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.5rem;
  margin-top: 0.6rem;
}
</style>
