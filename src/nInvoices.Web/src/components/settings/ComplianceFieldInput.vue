<template>
  <label v-if="field.type === 'Boolean'" class="toggle" :for="id">
    <input :id="id" type="checkbox" :checked="value === 'true'" @change="emit('update', ($event.target as HTMLInputElement).checked ? 'true' : '')" />
    <span>
      <strong>{{ field.label }}</strong>
      <small v-if="field.help">{{ field.help }}</small>
    </span>
  </label>

  <BaseField v-else :label="field.label" :for="id" :help="field.help ?? undefined" :error="error">
    <select v-if="field.type === 'Choice'" :id="id" class="control" :value="value" @change="emit('update', ($event.target as HTMLSelectElement).value)">
      <option value="">Select…</option>
      <option v-for="option in field.options ?? []" :key="option.value" :value="option.value">{{ option.label }}</option>
    </select>
    <input v-else :id="id" type="text" class="control" :value="value" @input="emit('update', ($event.target as HTMLInputElement).value)" />
  </BaseField>
</template>

<script setup lang="ts">
import type { ComplianceFieldDto } from '@/types'
import BaseField from '@/components/ui/BaseField.vue'

/**
 * One country-specific field, drawn from what the country module declares (text, choice or
 * yes/no), so a new country needs no new form code.
 */
defineProps<{
  field: ComplianceFieldDto
  /** The current value ('' when empty; 'true' for a ticked yes/no field) */
  value: string
  id: string
  error?: string | null
}>()

const emit = defineEmits<{ update: [value: string] }>()
</script>

<style scoped>
.toggle {
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
  padding: 0.7rem 0.8rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  cursor: pointer;
}

.toggle input {
  width: 1rem;
  height: 1rem;
  margin-top: 0.15rem;
  accent-color: var(--color-primary);
}

.toggle span {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  font-size: var(--text-md);
}

.toggle small {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}
</style>
