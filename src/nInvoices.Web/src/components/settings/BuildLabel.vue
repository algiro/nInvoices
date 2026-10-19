<template>
  <span>
    <template v-if="version">{{ version }} · </template>
    <a v-if="sha" :href="`${repositoryUrl}/commit/${sha}`" target="_blank" rel="noopener"><code>{{ commit }}</code></a>
    <span v-else class="muted">{{ t('settings.general.version.unknown') }}</span>
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{ version?: string; commit?: string | null }>()

const { t } = useI18n()

const repositoryUrl = 'https://github.com/algiro/nInvoices'

// "a1b2c3d-dirty" is shown as it is, but links to the commit it was built on
const sha = computed(() => props.commit?.replace(/-dirty$/, '') || null)
</script>

<style scoped>
code {
  padding: 0.05rem 0.3rem;
  border-radius: var(--radius-sm);
  background: var(--color-surface-sunken);
  font-size: 0.85em;
}

.muted {
  font-weight: 400;
  color: var(--color-text-muted);
}
</style>
