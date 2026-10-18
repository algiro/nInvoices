<template>
  <section class="section">
    <header class="section-header">
      <div>
        <h3>{{ t('customers.tabs.taxes') }}</h3>
        <p class="help">{{ t('taxes.help') }}</p>
      </div>
      <BaseButton variant="primary" icon="plus" @click="handleAdd">{{ t('taxes.new') }}</BaseButton>
    </header>

    <LoadingState v-if="loading && taxes.length === 0" :label="t('taxes.loading')" />

    <EmptyState v-else-if="error" icon="alert" :title="t('taxes.loadFailed')" :description="error" compact>
      <BaseButton @click="loadTaxes">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="taxes.length === 0"
      icon="percent"
      :title="t('taxes.emptyTitle')"
      :description="t('taxes.emptyDescription')"
      compact
    >
      <BaseButton variant="primary" icon="plus" @click="handleAdd">{{ t('taxes.addOne') }}</BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col" class="num">{{ t('taxes.order') }}</th>
            <th scope="col">{{ t('taxes.tax') }}</th>
            <th scope="col">{{ t('taxes.calculation') }}</th>
            <th scope="col" class="num">{{ t('rates.rate') }}</th>
            <th scope="col">{{ t('taxes.appliedTo') }}</th>
            <th scope="col" class="actions"><span class="sr-only">{{ t('common.actions') }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="tax in sortedTaxes" :key="tax.id">
            <td class="num muted">{{ tax.order }}</td>
            <td class="primary-cell">
              {{ tax.description }}
              <StatusPill v-if="!tax.isActive" class="inactive-pill">{{ t('common.inactive') }}</StatusPill>
            </td>
            <td>{{ handlerName(tax.handlerId) }}</td>
            <td class="num primary-cell">{{ formatRate(tax) }}</td>
            <td class="muted">{{ tax.appliedToTaxId ? getTaxName(tax.appliedToTaxId) : t('invoiceWizard.review.subtotal') }}</td>
            <td class="actions">
              <BaseButton size="sm" variant="ghost" icon="edit" @click="handleEdit(tax)">{{ t('common.edit') }}</BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                :title="t('common.delete')"
                :aria-label="t('common.deleteNamed', { name: tax.description })"
                @click="handleDelete(tax)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <BaseDialog
      :open="showForm"
      :title="editingTax ? t('taxes.edit') : t('taxes.new')"
      :close-on-overlay="false"
      @close="handleCloseForm"
    >
      <TaxForm
        v-if="showForm"
        :customer-id="customerId"
        :tax-id="editingTax?.id"
        @success="handleFormSuccess"
        @cancel="handleCloseForm"
      />
    </BaseDialog>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useTaxesStore } from '@/stores/taxes'
import TaxForm from './TaxForm.vue'
import type { TaxDto } from '@/types'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { currentLanguage } from '@/i18n'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface Props {
  customerId: number
}

const props = defineProps<Props>()
const taxesStore = useTaxesStore()
const toast = useToast()
const { confirm } = useConfirm()

const showForm = ref(false)
const editingTax = ref<TaxDto | null>(null)

const taxes = computed(() => taxesStore.taxesByCustomer(props.customerId))
const loading = computed(() => taxesStore.loading)
const error = computed(() => taxesStore.error)
const sortedTaxes = computed(() => [...taxes.value].sort((a, b) => a.order - b.order))

onMounted(() => {
  loadTaxes()
})

async function loadTaxes() {
  try {
    await taxesStore.fetchByCustomerId(props.customerId)
  } catch {
    // the store keeps the message; the empty state shows it
  }
}

function handlerName(handlerId: string): string {
  return handlerId === 'FIXED_AMOUNT' ? t('taxes.handlers.fixed') : handlerId === 'COMPOUND' ? t('taxes.handlers.compound') : handlerId === 'PERCENTAGE' ? t('taxes.handlers.percentage') : handlerId
}

function formatRate(tax: TaxDto): string {
  const value = tax.rate.toLocaleString(currentLanguage(), { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  return tax.handlerId === 'FIXED_AMOUNT' ? value : `${value}%`
}

function getTaxName(taxId: number | null): string {
  if (!taxId) return t('invoiceWizard.review.subtotal')
  const tax = taxesStore.getTaxById(taxId)
  return tax ? tax.description : t('taxes.unknown')
}

function handleAdd() {
  editingTax.value = null
  showForm.value = true
}

function handleEdit(tax: TaxDto) {
  editingTax.value = tax
  showForm.value = true
}

async function handleDelete(tax: TaxDto) {
  if (!(await confirm({ title: t('taxes.deleteTitle'), message: t('taxes.deleteMessage', { name: tax.description }), confirmLabel: t('common.delete'), tone: 'danger' }))) {
    return
  }

  try {
    await taxesStore.remove(tax.id)
    toast.success(t('taxes.deleted'))
  } catch (error) {
    toast.failure(t('taxes.deleteFailed'), error)
  }
}

function handleFormSuccess() {
  toast.success(editingTax.value ? t('taxes.updated') : t('taxes.added'))
  showForm.value = false
  editingTax.value = null
  loadTaxes()
}

function handleCloseForm() {
  showForm.value = false
  editingTax.value = null
}
</script>

<style scoped>
.section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.section-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
}

.section-header h3 {
  font-size: 1rem;
}

.help {
  margin: 0.2rem 0 0;
  max-width: 65ch;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.inactive-pill {
  margin-left: 0.4rem;
}
</style>
