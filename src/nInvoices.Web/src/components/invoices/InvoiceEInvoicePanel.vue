<template>
  <BasePanel
    v-if="formats.length"
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
          {{ format.fileName }} · generated {{ formatDate(format.generatedAt, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) }}
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
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { invoicesApi } from '@/api/invoices'
import type { ComplianceIssueDto, EInvoiceGenerationDto, InvoiceEInvoiceDto } from '@/types'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import { useToast } from '@/composables/useToast'
import { formatDate } from '@/utils/format'

const props = defineProps<{
  invoiceId: number
  /** The invoice status: formats can only be generated once it is finalized (and not cancelled) */
  status: string
}>()

const toast = useToast()
const formats = ref<InvoiceEInvoiceDto[]>([])
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
