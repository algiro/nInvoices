<template>
  <fieldset v-if="isOn" class="chain">
    <legend>{{ t('countries.es.chain.title') }}</legend>
    <p class="intro">
      {{ t('countries.es.chain.intro') }}
    </p>
    <div v-if="sendStatus" class="row">
      <StatusPill :tone="sendStatus.rejected > 0 ? 'danger' : sendStatus.pending > 0 ? 'neutral' : 'success'">
        {{ t('countries.es.chain.status', { pending: sendStatus.pending, registered: sendStatus.accepted + sendStatus.acceptedWithErrors, rejected: sendStatus.rejected }) }}
      </StatusPill>
      <BaseButton :loading="sendBusy" :disabled="sendBusy || sendStatus.pending === 0" @click="sendNow">{{ t('countries.es.chain.sendNow') }}</BaseButton>
      <p v-if="sendStatus.firstProblem" class="problems">{{ sendStatus.firstProblem }}</p>
    </div>
    <div class="row">
      <BaseButton :loading="chainBusy" :disabled="chainBusy" @click="verifyChain">{{ t('countries.es.chain.verify') }}</BaseButton>
      <StatusPill v-if="chainReport" :tone="chainReport.isIntact ? 'success' : 'danger'">
        {{ chainReport.isIntact ? t('countries.es.chain.intact', chainReport.records) : t('countries.es.chain.problems') }}
      </StatusPill>
    </div>
    <ul v-if="chainReport && !chainReport.isIntact" class="problems">
      <li v-for="problem in chainReport.problems" :key="`${problem.sequence}-${problem.message}`">
        <strong>{{ t('countries.es.chain.recordN', { n: problem.sequence }) }}</strong> {{ problem.message }}
      </li>
    </ul>
  </fieldset>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { ComplianceCountryDto } from '@/types'
import type { ChainReportDto, VerifactuStatusDto } from './types'
import { verifactuApi } from './verifactuApi'
import { useToast } from '@/composables/useToast'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps<{
  /** Spain's rules and the user's saved settings */
  country: ComplianceCountryDto
}>()

const toast = useToast()

// As saved: the chain exists once Verifactu is turned on, not while the box is only ticked
const isOn = computed(() => props.country.settings.values.verifactu === 'true')

// What the Tax Agency has registered of the records
const sendStatus = ref<VerifactuStatusDto | null>(null)
const sendBusy = ref(false)

async function loadSendStatus() {
  try {
    sendStatus.value = await verifactuApi.getStatus()
  } catch {
    sendStatus.value = null // secondary: the rest of the settings work without it
  }
}

async function sendNow() {
  sendBusy.value = true
  try {
    const run = await verifactuApi.submit()
    if (run.problem) toast.failure(t('countries.es.chain.notAllSent'), new Error(run.problem))
    else if (run.sent > 0) toast.success(t('countries.es.chain.sent', run.sent))
    else toast.success(t('countries.es.chain.nothingToSend'), { message: t('countries.es.chain.pause') })
  } catch (err) {
    toast.failure(t('countries.es.chain.sendFailed'), err)
  } finally {
    sendBusy.value = false
    await loadSendStatus()
  }
}

// The check of the record chain
const chainReport = ref<ChainReportDto | null>(null)
const chainBusy = ref(false)

async function verifyChain() {
  chainBusy.value = true
  try {
    chainReport.value = await verifactuApi.verifyChain()
  } catch (err) {
    toast.failure(t('countries.es.chain.verifyFailed'), err)
  } finally {
    chainBusy.value = false
  }
}

watch(isOn, on => { if (on) loadSendStatus() }, { immediate: true })
</script>

<style scoped>
.chain {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 0.75rem 0.9rem 0.9rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.chain legend {
  padding: 0 0.3rem;
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--color-text-secondary);
}

.intro {
  margin: 0;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.problems {
  margin: 0;
  padding-left: 1.1rem;
  font-size: var(--text-sm);
  color: var(--color-danger);
}
</style>
