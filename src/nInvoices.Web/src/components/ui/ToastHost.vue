<template>
  <div class="toast-stack" aria-live="polite" aria-relevant="additions">
    <TransitionGroup name="toast">
      <div
        v-for="toast in toasts"
        :key="toast.id"
        class="toast"
        :class="`tone-${toast.tone}`"
        :role="toast.tone === 'error' ? 'alert' : 'status'"
      >
        <AppIcon :name="iconFor(toast.tone)" class="toast-icon" />
        <div class="toast-text">
          <strong>{{ toast.title }}</strong>
          <span v-if="toast.message">{{ toast.message }}</span>
        </div>
        <button type="button" class="toast-close" aria-label="Dismiss" @click="dismiss(toast.id)">
          <AppIcon name="close" />
        </button>
      </div>
    </TransitionGroup>
  </div>
</template>

<script setup lang="ts">
import AppIcon, { type IconName } from './AppIcon.vue'
import { useToast, type ToastTone } from '@/composables/useToast'

const { toasts, dismiss } = useToast()

function iconFor(tone: ToastTone): IconName {
  return tone === 'success' ? 'success' : tone === 'error' ? 'error' : tone === 'warning' ? 'alert' : 'info'
}
</script>

<style scoped>
.toast-stack {
  position: fixed;
  right: 1rem;
  bottom: 1rem;
  z-index: 1100;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  width: min(24rem, calc(100vw - 2rem));
  pointer-events: none;
}

.toast {
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
  padding: 0.7rem 0.75rem 0.7rem 0.85rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-left-width: 3px;
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-md);
  font-size: var(--text-md);
  pointer-events: auto;
}

.tone-success { border-left-color: var(--color-success); }
.tone-success .toast-icon { color: var(--color-success); }
.tone-error { border-left-color: var(--color-danger); }
.tone-error .toast-icon { color: var(--color-danger); }
.tone-warning { border-left-color: var(--color-warning); }
.tone-warning .toast-icon { color: var(--color-warning); }
.tone-info { border-left-color: var(--color-primary); }
.tone-info .toast-icon { color: var(--color-primary); }

.toast-icon {
  margin-top: 0.1rem;
}

.toast-text {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  min-width: 0;
  overflow-wrap: anywhere;
}

.toast-text strong {
  font-weight: 600;
  color: var(--color-text);
}

.toast-text span {
  color: var(--color-text-muted);
}

.toast-close {
  display: inline-flex;
  padding: 0.15rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-subtle);
  cursor: pointer;
}

.toast-close:hover {
  background: var(--color-surface-sunken);
  color: var(--color-text);
}

.toast-enter-active,
.toast-leave-active {
  transition: opacity 0.18s ease, transform 0.18s ease;
}

.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateY(6px);
}
</style>
