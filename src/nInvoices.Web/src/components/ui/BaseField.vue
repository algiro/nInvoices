<template>
  <div class="field" :class="{ 'has-error': !!error }">
    <label v-if="label" :for="for" class="label">
      {{ label }}
      <span v-if="required" class="required" aria-hidden="true">*</span>
      <span v-else-if="optional" class="optional">optional</span>
    </label>
    <slot />
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <p v-else-if="help" class="help">{{ help }}</p>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  label?: string
  /** id of the control the label points to */
  for?: string
  required?: boolean
  optional?: boolean
  help?: string
  error?: string | null
}>()
</script>

<style scoped>
.field {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  min-width: 0;
}

.label {
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--color-text-secondary);
}

.required {
  margin-left: 0.15rem;
  color: var(--color-danger);
}

.optional {
  margin-left: 0.35rem;
  font-weight: 400;
  color: var(--color-text-subtle);
}

.help,
.error {
  margin: 0;
  font-size: var(--text-sm);
}

.help {
  color: var(--color-text-muted);
}

.error {
  color: var(--color-danger);
}

.has-error :deep(.control) {
  border-color: var(--color-danger);
}
</style>
