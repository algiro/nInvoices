<template>
  <svg
    class="app-icon"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.8"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    focusable="false"
  >
    <path v-for="(d, i) in paths[name]" :key="i" :d="d" />
  </svg>
</template>

<script lang="ts">
// Re-exported so components can keep importing the type from here
export type { IconName } from './icons'
</script>

<script setup lang="ts">
import { iconPaths as paths, type IconName } from './icons'

defineProps<{ name: IconName }>()
</script>

<style scoped>
.app-icon {
  width: 1.1rem;
  height: 1.1rem;
  flex: none;
}
</style>
