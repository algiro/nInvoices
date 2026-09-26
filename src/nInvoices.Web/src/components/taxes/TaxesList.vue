<template>
  <section class="section">
    <header class="section-header">
      <div>
        <h3>Taxes</h3>
        <p class="help">Lines added below the subtotal, calculated in order. A negative rate subtracts, e.g. withholding tax.</p>
      </div>
      <BaseButton variant="primary" icon="plus" @click="handleAdd">New tax</BaseButton>
    </header>

    <LoadingState v-if="loading && taxes.length === 0" label="Loading taxes…" />

    <EmptyState v-else-if="error" icon="alert" title="Taxes could not be loaded" :description="error" compact>
      <BaseButton @click="loadTaxes">Try again</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="taxes.length === 0"
      icon="percent"
      title="No taxes"
      description="Invoices for this customer show only the subtotal. Add VAT or other taxes if they apply."
      compact
    >
      <BaseButton variant="primary" icon="plus" @click="handleAdd">Add a tax</BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col" class="num">Order</th>
            <th scope="col">Tax</th>
            <th scope="col">Calculation</th>
            <th scope="col" class="num">Rate</th>
            <th scope="col">Applied to</th>
            <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="tax in sortedTaxes" :key="tax.id">
            <td class="num muted">{{ tax.order }}</td>
            <td class="primary-cell">
              {{ tax.description }}
              <StatusPill v-if="!tax.isActive" class="inactive-pill">Inactive</StatusPill>
            </td>
            <td>{{ handlerName(tax.handlerId) }}</td>
            <td class="num primary-cell">{{ formatRate(tax) }}</td>
            <td class="muted">{{ tax.appliedToTaxId ? getTaxName(tax.appliedToTaxId) : 'Subtotal' }}</td>
            <td class="actions">
              <BaseButton size="sm" variant="ghost" icon="edit" @click="handleEdit(tax)">Edit</BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                title="Delete"
                :aria-label="`Delete ${tax.description}`"
                @click="handleDelete(tax)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <BaseDialog
      :open="showForm"
      :title="editingTax ? 'Edit tax' : 'New tax'"
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
  return handlerId === 'FIXED_AMOUNT' ? 'Fixed amount' : handlerId === 'COMPOUND' ? 'Compound' : handlerId === 'PERCENTAGE' ? 'Percentage' : handlerId
}

function formatRate(tax: TaxDto): string {
  const value = tax.rate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  return tax.handlerId === 'FIXED_AMOUNT' ? value : `${value}%`
}

function getTaxName(taxId: number | null): string {
  if (!taxId) return 'Subtotal'
  const tax = taxesStore.getTaxById(taxId)
  return tax ? tax.description : 'Unknown tax'
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
  if (!(await confirm({ title: 'Delete tax?', message: `"${tax.description}" will be permanently deleted.`, confirmLabel: 'Delete', tone: 'danger' }))) {
    return
  }

  try {
    await taxesStore.remove(tax.id)
    toast.success('Tax deleted')
  } catch (error: any) {
    toast.failure('Failed to delete tax', error)
  }
}

function handleFormSuccess() {
  toast.success(editingTax.value ? 'Tax updated' : 'Tax added')
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
