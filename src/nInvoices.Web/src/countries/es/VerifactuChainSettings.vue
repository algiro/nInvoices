<template>
  <fieldset v-if="isOn" class="chain">
    <legend>Record chain</legend>
    <p class="intro">
      Every invoice you issue is added to a chain in which each record holds the hash of the one before. Check it any time: a record
      changed or removed afterwards shows up here.
    </p>
    <div v-if="sendStatus" class="row">
      <StatusPill :tone="sendStatus.rejected > 0 ? 'danger' : sendStatus.pending > 0 ? 'neutral' : 'success'">
        {{ sendStatus.pending }} waiting · {{ sendStatus.accepted + sendStatus.acceptedWithErrors }} registered · {{ sendStatus.rejected }} rejected
      </StatusPill>
      <BaseButton :loading="sendBusy" :disabled="sendBusy || sendStatus.pending === 0" @click="sendNow">Send now</BaseButton>
      <p v-if="sendStatus.firstProblem" class="problems">{{ sendStatus.firstProblem }}</p>
    </div>
    <div class="row">
      <BaseButton :loading="chainBusy" :disabled="chainBusy" @click="verifyChain">Check the chain</BaseButton>
      <StatusPill v-if="chainReport" :tone="chainReport.isIntact ? 'success' : 'danger'">
        {{ chainReport.isIntact ? `Intact: ${chainReport.records} record${chainReport.records === 1 ? '' : 's'}` : 'Problems found' }}
      </StatusPill>
    </div>
    <ul v-if="chainReport && !chainReport.isIntact" class="problems">
      <li v-for="problem in chainReport.problems" :key="`${problem.sequence}-${problem.message}`">
        <strong>Record {{ problem.sequence }}:</strong> {{ problem.message }}
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
    if (run.problem) toast.failure('Not everything could be sent', new Error(run.problem))
    else if (run.sent > 0) toast.success(`Sent ${run.sent} record${run.sent === 1 ? '' : 's'} to the Tax Agency`)
    else toast.success('Nothing to send right now', { message: 'The Tax Agency asks for a pause between submissions.' })
  } catch (err) {
    toast.failure('Could not send the records', err)
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
    toast.failure('Could not check the chain', err)
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
