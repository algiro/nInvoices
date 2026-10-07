<template>
  <BasePanel
    v-if="info?.isRecorded"
    title="Verifactu"
    description="This invoice is recorded in your tamper-evident chain of invoices, and shows a QR code anyone can use to check it with the Tax Agency."
  >
    <div class="qr-block">
      <p class="heading">{{ info.qrHeading }}</p>
      <!-- Drawn by the server from the invoice data; not user input -->
      <!-- eslint-disable-next-line vue/no-v-html -->
      <div class="qr" role="img" :aria-label="`${info.qrHeading} ${info.qrUrl}`" v-html="info.qrSvg"></div>
      <p class="legend">{{ info.legend }}</p>
    </div>

    <StatusPill v-if="info.isCancelled" tone="danger">Cancelled: recorded as cancelled</StatusPill>

    <div v-if="info.submissionStatus" class="submission">
      <StatusPill :tone="submissionTone">{{ submissionLabel }}</StatusPill>
      <p v-if="info.submissionMessage" class="message">{{ info.submissionMessage }}</p>
    </div>

    <dl class="facts">
      <div><dt>Record</dt><dd>No. {{ info.sequence }} in your chain</dd></div>
      <div v-if="info.csv"><dt>Tax Agency code</dt><dd class="mono">{{ info.csv }}</dd></div>
      <div><dt>Recorded</dt><dd class="mono">{{ info.generatedAt }}</dd></div>
      <div>
        <dt>Hash</dt>
        <dd class="mono hash" :title="info.hash ?? ''">{{ info.hash }}</dd>
      </div>
    </dl>

    <a v-if="info.qrUrl" :href="info.qrUrl" target="_blank" rel="noopener" class="check">Check it on the Tax Agency site</a>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { verifactuApi } from '@/api/verifactu'
import type { InvoiceVerifactuDto } from '@/types'
import BasePanel from '@/components/ui/BasePanel.vue'
import StatusPill from '@/components/ui/StatusPill.vue'

const props = defineProps<{
  invoiceId: number
  /** The invoice status: finalizing creates the record, so look again when it changes */
  status: string
}>()

const info = ref<InvoiceVerifactuDto | null>(null)

const submissionLabel = computed(() => {
  switch (info.value?.submissionStatus) {
    case 'Accepted': return 'Registered with the Tax Agency'
    case 'AcceptedWithErrors': return 'Registered, with remarks'
    case 'Rejected': return 'Rejected by the Tax Agency'
    default: return 'Waiting to be sent to the Tax Agency'
  }
})

const submissionTone = computed(() => {
  switch (info.value?.submissionStatus) {
    case 'Accepted': return 'success'
    case 'AcceptedWithErrors': return 'warning'
    case 'Rejected': return 'danger'
    default: return 'neutral'
  }
})

async function load() {
  try {
    info.value = await verifactuApi.getInvoice(props.invoiceId)
  } catch {
    info.value = null // secondary to the invoice itself: the page works without it
  }
}

watch(() => [props.invoiceId, props.status], load, { immediate: true })
</script>

<style scoped>
.qr-block {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.4rem;
  margin-bottom: 0.75rem;
  text-align: center;
}

.heading,
.legend {
  margin: 0;
  font-size: var(--text-sm);
  font-weight: 600;
}

/* The QR code must stay on a white ground to be readable, in the dark themes too */
.qr {
  width: 9.5rem;
  height: 9.5rem;
  padding: 0.35rem;
  background: #fff;
  border-radius: var(--radius-sm);
}

.qr :deep(svg) {
  width: 100%;
  height: 100%;
}

.submission {
  margin-top: 0.5rem;
}

.message {
  margin: 0.4rem 0 0;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
  overflow-wrap: anywhere;
}

.facts {
  margin: 0.75rem 0 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.facts div {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
}

.facts dt {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.facts dd {
  margin: 0;
  font-size: var(--text-sm);
  text-align: right;
  min-width: 0;
}

.mono {
  font-family: var(--font-mono);
}

.hash {
  overflow-wrap: anywhere;
  font-size: 0.7rem;
}

.check {
  display: inline-block;
  margin-top: 0.75rem;
  font-size: var(--text-sm);
}
</style>
