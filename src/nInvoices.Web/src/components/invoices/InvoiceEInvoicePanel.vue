<template>
  <BasePanel
    v-if="formats.length || channels.length"
    title="E-invoice"
    description="The structured, signed version of this invoice that some countries require, for example Facturae for Spanish public administrations."
  >
    <ul class="formats">
      <li v-for="format in formats" :key="format.formatId">
        <div class="line">
          <div class="what">
            <strong>{{ format.formatName }}</strong>
            <StatusPill v-if="format.isMandatory" tone="info">Required for this customer</StatusPill>
            <StatusPill v-if="format.isGenerated" tone="success">Generated</StatusPill>
          </div>
          <div class="buttons">
            <BaseButton
              v-if="format.isGenerated"
              icon="download"
              :loading="busy === `download:${format.formatId}`"
              :disabled="!!busy"
              @click="download(format)"
            >
              Download
            </BaseButton>
            <BaseButton
              v-if="canGenerate"
              :variant="format.isGenerated ? 'ghost' : 'primary'"
              :loading="busy === 'generate'"
              :disabled="!!busy"
              @click="generate"
            >
              {{ format.isGenerated ? 'Regenerate' : 'Generate' }}
            </BaseButton>
          </div>
        </div>

        <p v-if="format.isGenerated && format.generatedAt" class="muted">
          {{ format.fileName }} · generated {{ formatDate(format.generatedAt, dateTime) }}
        </p>
        <p v-else-if="!canGenerate" class="muted">
          {{ format.isMandatory ? 'Generated automatically when the invoice is finalized.' : 'Available once the invoice is finalized.' }}
        </p>

        <div v-if="issuesFor(format.formatId).length" class="issues" role="alert">
          <strong>It could not be generated:</strong>
          <ul>
            <li v-for="issue in issuesFor(format.formatId)" :key="issue.message">{{ issue.message }}</li>
          </ul>
        </div>
      </li>
    </ul>

    <ul v-if="channels.length" class="formats channels">
      <li v-for="channel in channels" :key="channel.channelId">
        <div class="line">
          <div class="what">
            <strong>{{ channel.displayName }}</strong>
            <StatusPill :tone="channel.environment === 'Production' ? 'warning' : 'neutral'">{{ channel.environment }}</StatusPill>
            <StatusPill v-if="channel.submission" tone="success">Sent</StatusPill>
          </div>
          <div class="buttons">
            <BaseButton
              v-if="channel.submission"
              variant="ghost"
              :loading="busy === `refresh:${channel.channelId}`"
              :disabled="!!busy"
              @click="refresh(channel)"
            >
              Refresh status
            </BaseButton>
            <BaseButton
              v-else
              variant="primary"
              :loading="busy === `send:${channel.channelId}`"
              :disabled="!!busy || !channel.canSend"
              @click="send(channel)"
            >
              Send to {{ channel.displayName }}
            </BaseButton>
          </div>
        </div>

        <template v-if="channel.submission">
          <p class="muted">
            Registry code <strong>{{ channel.submission.reference }}</strong>
            · sent {{ formatDate(channel.submission.submittedAt, dateTime) }}
          </p>
          <p class="muted">
            Status: {{ channel.submission.statusName ?? channel.submission.statusCode ?? 'unknown' }}
            <template v-if="channel.submission.checkedAt"> (checked {{ formatDate(channel.submission.checkedAt, dateTime) }})</template>
          </p>
          <p v-if="channel.submission.cancellationStatus" class="muted">Cancellation: {{ channel.submission.cancellationStatus }}</p>
          <div v-if="channel.submission.lastError" class="issues" role="alert">
            The status could not be refreshed: {{ channel.submission.lastError }}
          </div>
        </template>
        <ul v-else-if="channel.problems.length" class="muted problems">
          <li v-for="problem in channel.problems" :key="problem">{{ problem }}</li>
        </ul>
      </li>
    </ul>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { invoicesApi } from '@/api/invoices'
import type { ComplianceIssueDto, EInvoiceChannelDto, EInvoiceGenerationDto, InvoiceEInvoiceDto } from '@/types'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import { useToast } from '@/composables/useToast'
import { formatDate } from '@/utils/format'
import { useConfirm } from '@/composables/useConfirm'

const props = defineProps<{
  invoiceId: number
  /** The invoice status: formats can only be generated once it is finalized (and not cancelled) */
  status: string
}>()

const toast = useToast()
const { confirm } = useConfirm()
const dateTime = { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' } as const
const formats = ref<InvoiceEInvoiceDto[]>([])
const channels = ref<EInvoiceChannelDto[]>([])
const results = ref<EInvoiceGenerationDto[]>([])
const busy = ref<string | null>(null)

const canGenerate = computed(() => ['Finalized', 'Sent', 'Paid'].includes(props.status))

function issuesFor(formatId: string): ComplianceIssueDto[] {
  const result = results.value.find(r => r.formatId === formatId)
  return result && !result.generated ? result.issues : []
}

async function load() {
  try {
    formats.value = await invoicesApi.getEInvoices(props.invoiceId)
  } catch {
    formats.value = [] // secondary to the invoice itself: the page works without it
  }
  try {
    channels.value = await invoicesApi.getEInvoiceChannels(props.invoiceId)
  } catch {
    channels.value = []
  }
}

async function send(channel: EInvoiceChannelDto) {
  const production = channel.environment === 'Production'
  const confirmed = await confirm({
    title: `Send to ${channel.displayName}?`,
    message: production
      ? `The signed e-invoice goes to the public administration through ${channel.displayName}. It cannot be taken back, and the file can no longer be regenerated.`
      : `This is the test environment of ${channel.displayName}: nothing reaches a real administration. The file can no longer be regenerated.`,
    confirmLabel: 'Send',
    tone: production ? 'danger' : 'default'
  })
  if (!confirmed) return

  busy.value = `send:${channel.channelId}`
  try {
    await invoicesApi.sendEInvoice(props.invoiceId, channel.channelId)
    toast.success(`Sent to ${channel.displayName}`)
  } catch (err) {
    toast.failure(`${channel.displayName} did not accept the e-invoice`, err)
  } finally {
    busy.value = null
    await load()
  }
}

async function refresh(channel: EInvoiceChannelDto) {
  busy.value = `refresh:${channel.channelId}`
  try {
    await invoicesApi.refreshEInvoice(props.invoiceId, channel.channelId)
  } catch (err) {
    toast.failure(`Could not read the status from ${channel.displayName}`, err)
  } finally {
    busy.value = null
    await load()
  }
}

async function generate() {
  busy.value = 'generate'
  try {
    results.value = await invoicesApi.generateEInvoices(props.invoiceId)
    await load()
    if (results.value.every(r => r.generated)) toast.success('E-invoice generated')
  } catch (err) {
    toast.failure('Could not generate the e-invoice', err)
  } finally {
    busy.value = null
  }
}

async function download(format: InvoiceEInvoiceDto) {
  busy.value = `download:${format.formatId}`
  try {
    const { blob, fileName } = await invoicesApi.downloadEInvoice(props.invoiceId, format.formatId)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName ?? format.fileName ?? 'invoice.xml'
    link.click()
    URL.revokeObjectURL(url)
  } catch (err) {
    toast.failure('Could not download the e-invoice', err)
  } finally {
    busy.value = null
  }
}

// Finalizing generates the required formats on the server, so look again when the status changes
watch(() => [props.invoiceId, props.status], () => { results.value = []; load() }, { immediate: true })
</script>

<style scoped>
.formats {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.channels {
  margin-top: 1.25rem;
  padding-top: 1rem;
  border-top: 1px solid var(--color-border);
}

.problems {
  padding-left: 1.1rem;
}

.line {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.what {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.buttons {
  display: flex;
  gap: 0.5rem;
}

.muted {
  margin: 0.35rem 0 0;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
  overflow-wrap: anywhere;
}

.issues {
  margin-top: 0.6rem;
  padding: 0.6rem 0.8rem;
  border: 1px solid var(--color-danger-line);
  border-radius: var(--radius-md);
  background: var(--color-danger-soft);
  color: var(--color-danger);
  font-size: var(--text-sm);
}

.issues ul {
  margin: 0.3rem 0 0;
  padding-left: 1.1rem;
}
</style>
