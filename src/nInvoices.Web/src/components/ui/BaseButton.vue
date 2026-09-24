<template>
  <component
    :is="to ? RouterLink : 'button'"
    :to="to"
    :type="to ? undefined : type"
    class="base-btn"
    :class="[`variant-${variant}`, `size-${size}`, { loading, 'icon-only': iconOnly }]"
    :disabled="to ? undefined : disabled || loading"
    :aria-busy="loading || undefined"
  >
    <span v-if="loading" class="spinner" aria-hidden="true"></span>
    <AppIcon v-else-if="icon" :name="icon" class="btn-icon" />
    <span v-if="$slots.default" class="label"><slot /></span>
  </component>
</template>

<script setup lang="ts">
import { RouterLink, type RouteLocationRaw } from 'vue-router'
import AppIcon, { type IconName } from './AppIcon.vue'

withDefaults(defineProps<{
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'ghost-danger'
  size?: 'sm' | 'md'
  type?: 'button' | 'submit' | 'reset'
  icon?: IconName
  iconOnly?: boolean
  loading?: boolean
  disabled?: boolean
  /** Renders a router link styled as a button. */
  to?: RouteLocationRaw
}>(), {
  variant: 'secondary',
  size: 'md',
  type: 'button'
})
</script>

<style scoped>
.base-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  font-weight: 500;
  line-height: 1.25;
  white-space: nowrap;
  text-decoration: none;
  cursor: pointer;
  transition: background 0.12s, border-color 0.12s, color 0.12s, box-shadow 0.12s;
}

.base-btn:hover {
  text-decoration: none;
}

.base-btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.size-md { padding: 0.5rem 0.95rem; font-size: var(--text-md); }
.size-sm { padding: 0.3rem 0.65rem; font-size: var(--text-sm); }
.size-md.icon-only { padding: 0.5rem; }
.size-sm.icon-only { padding: 0.3rem; }

.variant-primary {
  background: var(--color-primary);
  border-color: var(--color-primary);
  color: var(--color-on-primary);
}
.variant-primary:hover:not(:disabled) {
  background: var(--color-primary-hover);
  border-color: var(--color-primary-hover);
}

.variant-secondary {
  background: var(--color-surface);
  border-color: var(--color-border-strong);
  color: var(--color-text-secondary);
}
.variant-secondary:hover:not(:disabled) {
  border-color: var(--color-primary-line);
  color: var(--color-primary);
}

.variant-ghost {
  background: transparent;
  color: var(--color-text-secondary);
}
.variant-ghost:hover:not(:disabled) {
  background: var(--color-surface-sunken);
  color: var(--color-text);
}

.variant-ghost-danger {
  background: transparent;
  color: var(--color-text-muted);
}
.variant-ghost-danger:hover:not(:disabled) {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.variant-danger {
  background: var(--color-danger);
  border-color: var(--color-danger);
  color: #ffffff;
}
.variant-danger:hover:not(:disabled) {
  background: var(--color-danger-hover);
  border-color: var(--color-danger-hover);
}

.btn-icon {
  width: 1rem;
  height: 1rem;
  flex: none;
}

.spinner {
  width: 0.9rem;
  height: 0.9rem;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
