<template>
  <span class="pill" :class="`tone-${tone}`"><slot /></span>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  tone?: 'neutral' | 'info' | 'success' | 'warning' | 'danger'
}>(), {
  tone: 'neutral'
})
</script>

<style scoped>
.pill {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  padding: 0.08rem 0.55rem;
  border: 1px solid;
  border-radius: 999px;
  font-size: var(--text-xs);
  font-weight: 600;
  line-height: 1.5;
  white-space: nowrap;
}

.tone-neutral { color: var(--color-text-muted); background: var(--color-surface-muted); border-color: var(--color-border-strong); }
.tone-info { color: var(--color-primary); background: var(--color-primary-soft); border-color: var(--color-primary-line); }
.tone-success { color: var(--color-success); background: var(--color-success-soft); border-color: var(--color-success-line); }
.tone-warning { color: var(--color-warning); background: var(--color-warning-soft); border-color: var(--color-warning-line); }
.tone-danger { color: var(--color-danger); background: var(--color-danger-soft); border-color: var(--color-danger-line); }
</style>
