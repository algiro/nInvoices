<template>
  <BasePanel
    :title="t('numbering.title')"
    :description="t('numbering.description')"
  >
    <LoadingState v-if="loading && !numbering" :label="t('numbering.loading')" />

    <EmptyState v-else-if="error && !numbering" icon="alert" :title="t('numbering.loadFailed')" :description="error" compact>
      <BaseButton @click="load">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <div v-else-if="numbering" class="numbering">
      <div class="next-number">
        <span class="label">{{ t('numbering.next') }}</span>
        <span class="value mono">{{ numbering.nextNumber }}</span>
        <i18n-t keypath="numbering.current" tag="span" class="hint"><template #value>{{ numbering.currentValue }}</template><template #pattern><code>{{ numbering.numberFormat }}</code></template></i18n-t>
      </div>

      <form class="numbering-form" novalidate @submit.prevent="handleSave">
        <BaseField
          :label="t('numbering.pattern')"
          for="numberFormat"
          :help="t('numbering.patternHelp', { pattern: numbering.defaultNumberFormat })"
          :error="patternError"
        >
          <input
            id="numberFormat"
            v-model="format"
            type="text"
            class="control mono"
            :placeholder="numbering.defaultNumberFormat"
          />
        </BaseField>

        <BaseField
          :label="t('numbering.sequence')"
          for="sequenceValue"
          :help="isLowering ? t('numbering.loweringHelp') : t('numbering.sequenceHelp')"
        >
          <input id="sequenceValue" v-model.number="value" type="number" min="1" class="control num" />
        </BaseField>

        <div class="actions">
          <BaseButton type="submit" variant="primary" :loading="saving" :disabled="!canSave">{{ t('common.save') }}</BaseButton>
          <BaseButton variant="ghost-danger" :disabled="saving" @click="handleReset">{{ t('numbering.resetEllipsis') }}</BaseButton>
        </div>
      </form>
    </div>

    <details class="tokens">
      <summary>{{ t('numbering.howItWorks') }}</summary>
      <table class="data-table compact">
        <thead><tr><th scope="col">{{ t('numbering.token') }}</th><th scope="col">{{ t('numbering.becomes') }}</th><th scope="col">{{ t('numbering.example') }}</th></tr></thead>
        <tbody>
          <tr v-for="token in tokens" :key="token.code">
            <td><code>{{ token.code }}</code></td>
            <td>{{ token.meaning }}</td>
            <td class="mono">{{ token.example }}</td>
          </tr>
        </tbody>
      </table>
    </details>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { invoicesApi } from '@/api'
import type { InvoiceNumberingDto } from '@/types'
import { useToast, errorMessage, apiStatus } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const toast = useToast()
const { confirm } = useConfirm()

const numbering = ref<InvoiceNumberingDto | null>(null)
const loading = ref(false)
const saving = ref(false)
const error = ref<string | null>(null)
const patternError = ref<string | undefined>()

// The form: an empty pattern means "use the default"
const format = ref('')
const value = ref<number | null>(null)

const isLowering = computed(() => !!numbering.value && !!value.value && value.value < numbering.value.currentValue)

const isDirty = computed(() =>
  !!numbering.value &&
  (value.value !== numbering.value.currentValue || format.value.trim() !== (numbering.value.customNumberFormat ?? ''))
)

const canSave = computed(() => isDirty.value && value.value !== null && Number.isInteger(value.value) && value.value >= 1)

const tokens = computed(() => {
  const now = new Date()
  const sequence = numbering.value?.currentValue ?? 1
  return [
    { code: '{YEAR}', meaning: t('numbering.tokens.year'), example: String(now.getFullYear()) },
    { code: '{YEAR:yy}', meaning: t('numbering.tokens.yearShort'), example: String(now.getFullYear()).slice(-2) },
    { code: '{MONTH}', meaning: t('numbering.tokens.month'), example: String(now.getMonth() + 1) },
    { code: '{MONTH:00}', meaning: t('numbering.tokens.monthPadded'), example: String(now.getMonth() + 1).padStart(2, '0') },
    { code: '{NUMBER}', meaning: t('numbering.tokens.number'), example: String(sequence) },
    { code: '{NUMBER:000}', meaning: t('numbering.tokens.numberPadded'), example: String(sequence).padStart(3, '0') },
    { code: '{CUSTOMER}', meaning: t('numbering.tokens.customer'), example: 'IT0123…' },
    { code: '{CUSTOMER:3}', meaning: t('numbering.tokens.customerShort'), example: 'IT0' }
  ]
})

function apply(result: InvoiceNumberingDto) {
  numbering.value = result
  format.value = result.customNumberFormat ?? ''
  value.value = result.currentValue
  patternError.value = undefined
}

async function load() {
  loading.value = true
  error.value = null
  try {
    apply(await invoicesApi.getNumbering())
  } catch (err) {
    error.value = errorMessage(err, t('numbering.loadFailed'))
  } finally {
    loading.value = false
  }
}

async function save(nextValue: number, successTitle: string) {
  saving.value = true
  patternError.value = undefined
  try {
    const result = await invoicesApi.updateNumbering({
      value: nextValue,
      numberFormat: format.value.trim() || null
    })
    apply(result)
    toast.success(successTitle, { message: t('numbering.nextIs', { number: result.nextNumber }) })
  } catch (err) {
    // A bad pattern is the user's to fix in the field; anything else is a failure
    const message = errorMessage(err, t('numbering.updateFailed'))
    if (apiStatus(err) === 400) patternError.value = message
    else toast.failure(t('numbering.updateFailed'), err)
  } finally {
    saving.value = false
  }
}

async function handleSave() {
  if (!canSave.value || numbering.value === null || value.value === null) return

  if (isLowering.value) {
    const confirmed = await confirm({
      title: t('numbering.lowerTitle'),
      message: t('numbering.lowerMessage', { from: numbering.value.currentValue, to: value.value }),
      confirmLabel: t('numbering.lower'),
      tone: 'danger'
    })
    if (!confirmed) return
  }

  await save(value.value, t('numbering.updated'))
}

async function handleReset() {
  const confirmed = await confirm({
    title: t('numbering.resetTitle'),
    message: t('numbering.resetMessage'),
    confirmLabel: t('numbering.reset'),
    tone: 'danger'
  })
  if (!confirmed) return

  await save(1, t('numbering.resetDone'))
}

onMounted(load)
</script>

<style scoped>
.mono {
  font-family: var(--font-mono);
}

code {
  padding: 0.05rem 0.3rem;
  border-radius: var(--radius-sm);
  background: var(--color-surface-sunken);
  font-size: 0.85em;
}

.numbering {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1.2fr);
  gap: 1.25rem;
  align-items: start;
}

@media (max-width: 760px) {
  .numbering {
    grid-template-columns: minmax(0, 1fr);
  }
}

.next-number {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  padding: 1rem 1.1rem;
  border-radius: var(--radius-md);
  background: var(--color-primary-soft);
}

.next-number .label {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.next-number .value {
  font-size: 1.6rem;
  font-weight: 650;
  color: var(--color-primary);
  overflow-wrap: anywhere;
}

.next-number .hint {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.numbering-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.numbering-form .num {
  max-width: 10rem;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.tokens {
  margin-top: 1.25rem;
  font-size: var(--text-md);
}

.tokens summary {
  cursor: pointer;
  color: var(--color-primary);
  font-weight: 500;
}

.data-table.compact td,
.data-table.compact th {
  padding: 0.4rem 0.7rem;
}
</style>
