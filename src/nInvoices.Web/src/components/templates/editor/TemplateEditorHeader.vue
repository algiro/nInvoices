<template>
  <header class="editor-header">
    <router-link :to="backLink" class="back">
      <AppIcon name="chevronRight" class="back-icon" />
      {{ customerId === null ? 'Shared templates' : (customerName || 'Customer') }} · {{ kindLabel }}
    </router-link>

    <div class="header-row">
      <input
        id="template-name"
        v-model="name"
        class="name-input"
        type="text"
        maxlength="200"
        :placeholder="kind === 'invoice' ? 'Untitled invoice template' : 'Template name (required)'"
        aria-label="Template name"
      />

      <label v-if="kind === 'invoice'" class="type-field">
        <span>Invoice type</span>
        <select
          id="template-invoice-type"
          v-model="invoiceType"
          :disabled="!isNew"
          :title="isNew ? '' : 'The invoice type is fixed once a template is created'"
        >
          <option value="Monthly">Monthly</option>
          <option value="OneTime">One-time</option>
        </select>
      </label>

      <span v-if="!isNew" class="pill" :class="isActive ? 'active' : 'inactive'">
        {{ isActive ? 'Active' : 'Inactive' }}
      </span>

      <span class="save-state" :class="{ dirty }">
        {{ saving ? 'Saving…' : dirty ? 'Unsaved changes' : isNew ? 'Not saved yet' : 'All changes saved' }}
      </span>

      <span class="grow"></span>

      <div class="layout-switch" role="group" aria-label="Layout">
        <button type="button" :class="{ active: showVariables }" :aria-pressed="showVariables" @click="showVariables = !showVariables">
          Variables
        </button>
        <button
          v-for="mode in LAYOUT_MODES"
          :key="mode.value"
          type="button"
          :class="{ active: layout === mode.value }"
          :aria-pressed="layout === mode.value"
          @click="layout = mode.value"
        >
          {{ mode.label }}
        </button>
      </div>

      <BaseButton variant="ghost" @click="emit('load-sample')">Load sample</BaseButton>
      <BaseButton variant="primary" :loading="saving" :disabled="loading" @click="emit('save')">
        Save <kbd class="kbd">Ctrl S</kbd>
      </BaseButton>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import type { TemplateKind } from './templateVariables'
import type { InvoiceTypeName } from './templateKinds'
import { LAYOUT_MODES, type EditorLayout } from './useEditorLayout'

const props = defineProps<{
  kind: TemplateKind
  /** null for a template shared by all customers */
  customerId: number | null
  customerName: string
  isNew: boolean
  isActive: boolean
  saving: boolean
  dirty: boolean
  loading: boolean
}>()

const emit = defineEmits<{ 'load-sample': []; save: [] }>()

const name = defineModel<string>('name', { required: true })
const invoiceType = defineModel<InvoiceTypeName>('invoiceType', { required: true })
const layout = defineModel<EditorLayout>('layout', { required: true })
const showVariables = defineModel<boolean>('showVariables', { required: true })

const kindLabel = computed(() =>
  props.kind === 'invoice' ? 'Invoice templates' : props.kind === 'email' ? 'Email templates' : 'Monthly reports')

const backLink = computed(() => props.customerId === null
  ? { path: '/templates', query: { tab: props.kind } }
  : {
      path: `/customers/${props.customerId}`,
      query: { tab: props.kind === 'invoice' ? 'templates' : props.kind === 'email' ? 'emails' : 'monthly-reports' }
    })
</script>

<style scoped>
.editor-header {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.back {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  width: fit-content;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.back-icon {
  width: 0.9rem;
  height: 0.9rem;
  transform: rotate(180deg);
}

.header-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.6rem 0.75rem;
}

.name-input {
  flex: 0 1 24rem;
  min-width: 12rem;
  padding: 0.3rem 0.5rem;
  margin-left: -0.5rem;
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  background: transparent;
  font-size: var(--text-lg);
  font-weight: 600;
  color: var(--color-text);
}

.name-input:hover {
  border-color: var(--color-border);
}

.name-input:focus {
  outline: none;
  border-color: var(--color-primary);
  background: var(--color-surface);
  box-shadow: var(--focus-ring);
}

.type-field {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.type-field select {
  padding: 0.3rem 0.45rem;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  font-size: var(--text-sm);
}

.pill {
  font-size: var(--text-xs);
  font-weight: 600;
  padding: 0.1rem 0.55rem;
  border-radius: 999px;
  border: 1px solid;
}

.pill.active {
  color: var(--color-success);
  background: var(--color-success-soft);
  border-color: var(--color-success-line);
}

.pill.inactive {
  color: var(--color-text-muted);
  background: var(--color-surface-muted);
  border-color: var(--color-border-strong);
}

.save-state {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.save-state.dirty {
  color: var(--color-warning);
  font-weight: 500;
}

.grow {
  flex: 1;
}

.layout-switch {
  display: inline-flex;
  gap: 2px;
  padding: 2px;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
}

.layout-switch button {
  padding: 0.25rem 0.6rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
}

.layout-switch button:hover {
  background: var(--color-surface-sunken);
  color: var(--color-text);
}

.layout-switch button.active {
  background: var(--color-primary);
  color: var(--color-on-primary);
}

.kbd {
  margin-left: 0.25rem;
  padding: 0 0.3rem;
  border-radius: var(--radius-sm);
  background: color-mix(in srgb, var(--color-on-primary) 16%, transparent);
  font-size: 0.68rem;
  font-family: var(--font-mono);
}
</style>
