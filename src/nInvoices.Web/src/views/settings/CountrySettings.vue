<template>
  <CountryPanel @country="code => (country = code)" />

  <!-- Keyed by the country: switching reloads both for the new one -->
  <template v-if="country">
    <ComplianceSettingsPanel :key="`rules-${country}`" :country-code="country" />
    <TaxProfilePanel :key="`taxes-${country}`" :country-code="country" />
  </template>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import CountryPanel from '@/components/settings/CountryPanel.vue'
import ComplianceSettingsPanel from '@/components/settings/ComplianceSettingsPanel.vue'
import TaxProfilePanel from '@/components/settings/TaxProfilePanel.vue'

// Null until the user has a country: then nothing country-specific is shown
const country = ref<string | null>(null)
</script>
