<template>
  <div class="shared-templates">
    <PageHeader
      :title="t('routes.sharedTemplates')"
      :subtitle="t('templates.sharedSubtitle')"
    />

    <BaseTabs :model-value="activeTab" :tabs="tabs" :label="t('templates.sharedKinds')" @update:model-value="selectTab" />

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
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const route = useRoute()
const router = useRouter()

const tabs = computed((): { id: TemplateKind; label: string }[] => [
  { id: 'invoice', label: t('customers.tabs.templates') },
  { id: 'monthly-report', label: t('customers.tabs.monthlyReports') },
  { id: 'email', label: t('customers.tabs.emails') }
])

// The open tab lives in the URL (?tab=…) so the editor's "back" link lands on it
const activeTab = computed((): TemplateKind => {
  const tab = route.query.tab
  return tabs.value.find(item => item.id === tab)?.id ?? 'invoice'
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
