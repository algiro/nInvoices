<template>
  <div class="shared-templates">
    <PageHeader
      title="Shared templates"
      subtitle="Templates shared by all your customers. A customer uses the shared template unless it has an active one of its own."
    />

    <BaseTabs :model-value="activeTab" :tabs="tabs" label="Kinds of shared template" @update:model-value="selectTab" />

    <div class="tab-body" role="tabpanel" :aria-labelledby="`tab-${activeTab}`">
      <BasePanel>
        <TemplateListPanel :key="activeTab" :kind="activeTab" :customer-id="null" />
      </BasePanel>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseTabs from '@/components/ui/BaseTabs.vue'
import TemplateListPanel from '@/components/templates/TemplateListPanel.vue'
import type { TemplateKind } from '@/components/templates/editor/templateVariables'

const route = useRoute()
const router = useRouter()

const tabs: { id: TemplateKind; label: string }[] = [
  { id: 'invoice', label: 'Invoice templates' },
  { id: 'monthly-report', label: 'Monthly reports' },
  { id: 'email', label: 'Email templates' }
]

// The open tab lives in the URL (?tab=…) so the editor's "back" link lands on it
const activeTab = computed((): TemplateKind => {
  const tab = route.query.tab
  return tabs.find(t => t.id === tab)?.id ?? 'invoice'
})

function selectTab(id: string) {
  router.replace({ query: { ...route.query, tab: id === 'invoice' ? undefined : id } })
}
</script>

<style scoped>
.tab-body {
  margin-top: 1.25rem;
}
</style>
