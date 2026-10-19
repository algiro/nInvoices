<template>
  <BasePanel :title="t('settings.general.version.title')" :description="t('settings.general.version.description')">
    <dl class="facts">
      <div>
        <dt>{{ t('settings.general.version.web') }}</dt>
        <dd><BuildLabel :commit="webCommit" /></dd>
      </div>
      <div>
        <dt>{{ t('settings.general.version.api') }}</dt>
        <dd>
          <BuildLabel v-if="api" :version="api.version" :commit="api.commit" />
          <span v-else-if="failed" class="muted">{{ t('settings.general.version.failed') }}</span>
        </dd>
      </div>
    </dl>
    <p v-if="mismatch" class="note">{{ t('settings.general.version.mismatch') }}</p>
  </BasePanel>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BasePanel from '@/components/ui/BasePanel.vue'
import BuildLabel from '@/components/settings/BuildLabel.vue'
import { versionApi } from '@/api'
import type { AppVersionDto } from '@/types'

const { t } = useI18n()

const webCommit = __APP_COMMIT__ || null
const api = ref<AppVersionDto | null>(null)
const failed = ref(false)

// Both are built from the same commit when deployed together; a difference means a stale page or a partial deploy
const mismatch = computed(() => !!api.value?.commit && !!webCommit && api.value.commit !== webCommit)

onMounted(async () => {
  try {
    api.value = await versionApi.get()
  } catch {
    failed.value = true
  }
})
</script>

<style scoped>
.facts {
  margin: 0;
}

.facts div {
  display: flex;
  gap: 1rem;
}

.facts dt {
  min-width: 8rem;
  color: var(--color-text-muted);
}

.facts dd {
  margin: 0;
  font-weight: 600;
  color: var(--color-text);
}

.muted {
  font-weight: 400;
  color: var(--color-text-muted);
}

.note {
  margin: 0.75rem 0 0;
  font-size: var(--text-md);
  color: var(--color-warning, var(--color-text-muted));
}
</style>
