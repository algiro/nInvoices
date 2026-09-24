<template>
  <Teleport to="body">
    <Transition name="dialog">
      <div v-if="open" class="overlay" @mousedown.self="onOverlay">
        <div
          ref="panel"
          class="dialog"
          :class="`size-${size}`"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="titleId"
          tabindex="-1"
          @keydown.esc.stop="emit('close')"
        >
          <header class="dialog-header">
            <div>
              <h2 :id="titleId" class="dialog-title">{{ title }}</h2>
              <p v-if="description" class="dialog-description">{{ description }}</p>
            </div>
            <button type="button" class="close-btn" aria-label="Close" @click="emit('close')">
              <AppIcon name="close" />
            </button>
          </header>
          <div class="dialog-body">
            <slot />
          </div>
          <footer v-if="$slots.footer" class="dialog-footer">
            <slot name="footer" />
          </footer>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, watch, nextTick, onBeforeUnmount } from 'vue'
import AppIcon from './AppIcon.vue'

const props = withDefaults(defineProps<{
  open: boolean
  title: string
  description?: string
  size?: 'sm' | 'md' | 'lg' | 'xl'
  /** Clicking the backdrop closes the dialog unless disabled (e.g. forms with unsaved input). */
  closeOnOverlay?: boolean
}>(), {
  size: 'md',
  closeOnOverlay: true
})

const emit = defineEmits<{ (e: 'close'): void }>()

const panel = ref<HTMLElement | null>(null)
const titleId = `dialog-title-${Math.random().toString(36).slice(2, 9)}`
let previouslyFocused: HTMLElement | null = null

function onOverlay() {
  if (props.closeOnOverlay) emit('close')
}

watch(
  () => props.open,
  async isOpen => {
    if (isOpen) {
      previouslyFocused = document.activeElement as HTMLElement | null
      document.body.style.overflow = 'hidden'
      await nextTick()
      // focus the first field if there is one, otherwise the panel itself
      const first = panel.value?.querySelector<HTMLElement>('input, select, textarea, [data-autofocus]')
      ;(first ?? panel.value)?.focus()
    } else {
      document.body.style.overflow = ''
      previouslyFocused?.focus?.()
    }
  },
  { immediate: true }
)

onBeforeUnmount(() => {
  document.body.style.overflow = ''
})
</script>

<style scoped>
.overlay {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: 6vh 1rem 1rem;
  background: rgba(15, 20, 30, 0.45);
  overflow-y: auto;
}

.dialog {
  width: 100%;
  display: flex;
  flex-direction: column;
  max-height: 88vh;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
  outline: none;
}

.size-sm { max-width: 26rem; }
.size-md { max-width: 34rem; }
.size-lg { max-width: 48rem; }
.size-xl { max-width: 72rem; }

.dialog-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  padding: 1rem 1.25rem 0.75rem;
}

.dialog-title {
  font-size: var(--text-lg);
  font-weight: 600;
}

.dialog-description {
  margin: 0.25rem 0 0;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.close-btn {
  display: inline-flex;
  padding: 0.3rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}

.close-btn:hover {
  background: var(--color-surface-sunken);
  color: var(--color-text);
}

.dialog-body {
  padding: 0.25rem 1.25rem 1.25rem;
  overflow-y: auto;
  font-size: var(--text-md);
  color: var(--color-text-secondary);
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  padding: 0.85rem 1.25rem;
  border-top: 1px solid var(--color-border);
  background: var(--color-surface-muted);
  border-radius: 0 0 var(--radius-lg) var(--radius-lg);
}

.dialog-enter-active,
.dialog-leave-active {
  transition: opacity 0.15s ease;
}

.dialog-enter-active .dialog,
.dialog-leave-active .dialog {
  transition: transform 0.15s ease;
}

.dialog-enter-from,
.dialog-leave-to {
  opacity: 0;
}

.dialog-enter-from .dialog,
.dialog-leave-to .dialog {
  transform: translateY(8px);
}
</style>
