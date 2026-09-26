<template>
  <div class="invoice-generate">
    <PageHeader title="New invoice" subtitle="Choose the customer and period, enter the time and expenses, then check the invoice before generating it." />

    <nav class="stepper" aria-label="Steps">
      <ol>
        <li v-for="(step, index) in steps" :key="step.id">
          <button
            type="button"
            class="step"
            :class="{ current: step.id === currentId, done: index < currentIndex }"
            :aria-current="step.id === currentId ? 'step' : undefined"
            :disabled="!canOpen(index)"
            @click="goTo(index)"
          >
            <span class="step-number">
              <AppIcon v-if="index < currentIndex" name="check" />
              <template v-else>{{ index + 1 }}</template>
            </span>
            <span class="step-label">{{ step.label }}</span>
          </button>
        </li>
      </ol>
    </nav>

    <StepCustomerPeriod v-if="currentId === 'customer'" :draft="draft" />
    <StepTime v-else-if="currentId === 'time'" :draft="draft" />
    <StepExpenses v-else-if="currentId === 'expenses'" :draft="draft" :show-errors="showErrors" />
    <StepReview v-else :draft="draft" @blocker="previewBlocker = $event" />

    <div class="summary-bar">
      <dl class="summary">
        <template v-if="isMonthly">
          <div><dt>Period</dt><dd>{{ periodLabel }}</dd></div>
          <div><dt>Worked days</dt><dd>{{ totals.workedDays }}</dd></div>
          <div><dt>Hours</dt><dd>{{ totals.totalHours.toFixed(1) }}</dd></div>
        </template>
        <div v-if="expensesTotal"><dt>Expenses</dt><dd>{{ expensesTotal }}</dd></div>
        <div class="estimate"><dt>Estimated subtotal</dt><dd>{{ estimatedAmount }}</dd></div>
      </dl>
      <div class="summary-actions">
        <span v-if="currentBlocker" class="blocker">{{ currentBlocker }}</span>
        <BaseButton v-if="currentIndex === 0" :disabled="loading" @click="handleCancel">Cancel</BaseButton>
        <BaseButton v-else icon="arrowLeft" :disabled="loading" @click="goTo(currentIndex - 1)">Back</BaseButton>
        <BaseButton v-if="currentId !== 'review'" variant="primary" @click="next">
          Next: {{ steps[currentIndex + 1]?.label }}
        </BaseButton>
        <BaseButton v-else variant="primary" :loading="loading" :disabled="!!currentBlocker" @click="handleGenerate">Generate invoice</BaseButton>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useInvoicesStore } from '@/stores/invoices'
import { useInvoiceDraft } from '@/composables/useInvoiceDraft'
import { useToast } from '@/composables/useToast'
import StepCustomerPeriod from '@/components/invoices/wizard/StepCustomerPeriod.vue'
import StepTime from '@/components/invoices/wizard/StepTime.vue'
import StepExpenses from '@/components/invoices/wizard/StepExpenses.vue'
import StepReview from '@/components/invoices/wizard/StepReview.vue'
import PageHeader from '@/components/ui/PageHeader.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'

const router = useRouter()
const route = useRoute()
const toast = useToast()
const invoicesStore = useInvoicesStore()

const draft = useInvoiceDraft()
const {
  isMonthly,
  periodLabel,
  totals,
  expensesTotal,
  estimatedAmount,
  customerBlocker,
  timeBlocker,
  expensesBlocker,
  incompleteExpense,
  buildPayload
} = draft

type StepId = 'customer' | 'time' | 'expenses' | 'review'

const loading = ref(false)
const currentId = ref<StepId>('customer')
const showErrors = ref(false)
// Set by the review step: the rendered preview reports why the invoice can't be generated
const previewBlocker = ref<string | null>(null)

// A one-time invoice bills no time, so it has no Time step
const steps = computed(() => [
  { id: 'customer' as const, label: 'Customer & period', blocker: customerBlocker.value },
  ...(isMonthly.value ? [{ id: 'time' as const, label: 'Time', blocker: timeBlocker.value }] : []),
  { id: 'expenses' as const, label: 'Expenses', blocker: expensesBlocker.value },
  { id: 'review' as const, label: 'Review & generate', blocker: previewBlocker.value }
])

const currentIndex = computed(() => Math.max(0, steps.value.findIndex(s => s.id === currentId.value)))
const currentBlocker = computed(() => steps.value[currentIndex.value].blocker)

/** A step can be opened when every step before it is complete. */
function canOpen(index: number) {
  return steps.value.slice(0, index).every(s => !s.blocker)
}

function goTo(index: number) {
  const step = steps.value[index]
  if (!step || !canOpen(index)) return
  showErrors.value = false
  currentId.value = step.id
  nextTick(() => window.scrollTo({ top: 0, behavior: 'smooth' }))
}

function next() {
  if (currentBlocker.value) {
    showErrors.value = true
    if (currentId.value === 'expenses' && incompleteExpense.value >= 0) {
      document.getElementById(`expense-description-${incompleteExpense.value}`)?.focus()
    }
    return
  }
  goTo(currentIndex.value + 1)
}

onMounted(() => draft.initialize(Number(route.query.customerId) || undefined))

async function handleGenerate() {
  if (steps.value.some(s => s.blocker)) return

  try {
    loading.value = true
    const invoice = await invoicesStore.generate(buildPayload())
    router.push(`/invoices/${invoice.id}`)
  } catch (error: any) {
    toast.failure('Failed to generate invoice', error)
  } finally {
    loading.value = false
  }
}

function handleCancel() {
  router.push('/invoices')
}
</script>

<style scoped>
.invoice-generate {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 82rem;
}

.stepper ol {
  display: flex;
  gap: 0.35rem;
  margin: 0;
  padding: 0;
  list-style: none;
  overflow-x: auto;
  scrollbar-width: none;
}

.stepper li {
  flex: 1;
  min-width: max-content;
}

.step {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  width: 100%;
  padding: 0.55rem 0.8rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: var(--text-md);
  text-align: left;
  cursor: pointer;
}

.step:hover:not(:disabled) {
  border-color: var(--color-border-strong);
  color: var(--color-text);
}

.step:disabled {
  color: var(--color-text-subtle);
  cursor: not-allowed;
}

.step.current {
  border-color: var(--color-primary);
  color: var(--color-text);
  font-weight: 600;
  box-shadow: inset 0 -2px 0 var(--color-primary);
}

.step-number {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: none;
  width: 1.5rem;
  height: 1.5rem;
  border-radius: 999px;
  background: var(--color-surface-sunken);
  font-size: var(--text-sm);
  font-weight: 600;
}

.step-number .app-icon {
  width: 0.85rem;
  height: 0.85rem;
}

.step.current .step-number {
  background: var(--color-primary);
  color: var(--color-on-primary);
}

.step.done .step-number {
  background: var(--color-primary-soft);
  color: var(--color-primary);
}

@media (max-width: 640px) {
  .step:not(.current) .step-label {
    display: none;
  }
}

/* sticks to the bottom of the viewport while the step scrolls */
.summary-bar {
  position: sticky;
  bottom: 0;
  z-index: 20;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem 1.5rem;
  margin: 0 -0.25rem;
  padding: 0.8rem 1.1rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-up);
}

.summary {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem 1.6rem;
  margin: 0;
}

.summary div {
  display: flex;
  flex-direction: column;
}

.summary dt {
  font-size: var(--text-xs);
  color: var(--color-text-muted);
}

.summary dd {
  margin: 0;
  font-size: var(--text-base);
  font-weight: 600;
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}

.summary .estimate dd {
  font-size: 1.15rem;
  color: var(--color-primary);
}

.summary-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.blocker {
  font-size: var(--text-sm);
  color: var(--color-warning);
}
</style>
