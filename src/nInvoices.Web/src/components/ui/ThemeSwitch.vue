<template>
  <div class="theme-switch" :class="{ compact }" role="radiogroup" aria-label="Theme">
    <button
      v-for="option in options"
      :key="option.value"
      type="button"
      role="radio"
      class="option"
      :class="{ active: preference === option.value }"
      :aria-checked="preference === option.value"
      :title="option.hint"
      @click="setPreference(option.value)"
    >
      <AppIcon :name="option.icon" />
      <span>{{ option.label }}</span>
    </button>
  </div>
</template>

<script setup lang="ts">
import AppIcon from './AppIcon.vue'
import type { IconName } from './icons'
import { useTheme, type ThemePreference } from '@/composables/useTheme'

withDefaults(defineProps<{
  /** Smaller variant for menus. */
  compact?: boolean
}>(), {
  compact: false
})

const { preference, setPreference } = useTheme()

const options: { value: ThemePreference; label: string; icon: IconName; hint: string }[] = [
  { value: 'system', label: 'System', icon: 'monitor', hint: 'Follow the operating system setting' },
  { value: 'light', label: 'Light', icon: 'sun', hint: 'Always light' },
  { value: 'dark', label: 'Dark', icon: 'moon', hint: 'Always dark' }
]
</script>

<style scoped>
.theme-switch {
  display: inline-flex;
  gap: 2px;
  padding: 2px;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
}

.option {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.35rem 0.75rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-md);
  cursor: pointer;
}

.option:hover:not(.active) {
  background: var(--color-surface-sunken);
  color: var(--color-text);
}

.option.active {
  background: var(--color-primary);
  color: var(--color-on-primary);
  font-weight: 600;
}

.option .app-icon {
  width: 1rem;
  height: 1rem;
}

.compact {
  display: flex;
  width: 100%;
}

.compact .option {
  flex: 1;
  justify-content: center;
  padding: 0.3rem 0.4rem;
  font-size: var(--text-sm);
}
</style>
