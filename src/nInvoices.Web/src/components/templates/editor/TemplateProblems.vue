<template>
  <div class="problems" role="alert">
    <div class="problems-title">
      <AppIcon name="error" />
      {{ errors.length === 1 ? '1 problem' : `${errors.length} problems` }}: {{ hasSyntaxErrors ? 'fix before saving' : 'the preview can’t render with sample data' }}
    </div>
    <button
      v-for="(error, index) in errors"
      :key="index"
      type="button"
      class="problem"
      :disabled="!error.line"
      @click="error.line && emit('go-to', error.line, error.column)"
    >
      <span v-if="error.line" class="where">Line {{ error.line }}</span>
      <span class="what">{{ error.message }}</span>
    </button>
  </div>
</template>

<script setup lang="ts">
import AppIcon from '@/components/ui/AppIcon.vue'
import type { TemplateError } from './useTemplatePreview'

defineProps<{ errors: TemplateError[]; hasSyntaxErrors: boolean }>()

/** A problem with a position was clicked: show it in the editor. */
const emit = defineEmits<{ 'go-to': [line: number, column: number] }>()
</script>

<style scoped>
.problems {
  max-height: 9rem;
  overflow-y: auto;
  border-top: 1px solid var(--color-danger-line);
  background: var(--color-danger-soft);
  font-size: var(--text-sm);
}

.problems-title {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.4rem 0.75rem 0.2rem;
  font-weight: 600;
  color: var(--color-danger);
}

.problem {
  width: 100%;
  display: flex;
  gap: 0.6rem;
  padding: 0.25rem 0.75rem 0.25rem 2.1rem;
  border: 0;
  border-radius: 0;
  background: transparent;
  text-align: left;
  color: var(--color-text-secondary);
  cursor: pointer;
}

.problem:hover:not(:disabled) {
  background: color-mix(in srgb, var(--color-danger) 8%, transparent);
  color: var(--color-text);
}

.problem:disabled {
  cursor: default;
  opacity: 1;
}

.where {
  flex: none;
  font-family: var(--font-mono);
  font-size: var(--text-xs);
  color: var(--color-danger);
  padding-top: 0.1rem;
}

.what {
  overflow-wrap: anywhere;
}
</style>
