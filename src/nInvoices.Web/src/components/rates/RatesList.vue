<template>
  <section class="section">
    <header class="section-header">
      <div>
        <h3>Rates</h3>
        <p class="help">The price used to bill this customer. Monthly invoices use the daily rate first, then monthly, then hourly.</p>
      </div>
      <BaseButton variant="primary" icon="plus" @click="handleAdd">New rate</BaseButton>
    </header>

    <LoadingState v-if="loading && rates.length === 0" label="Loading rates…" />

    <EmptyState v-else-if="error" icon="alert" title="Rates could not be loaded" :description="error" compact>
      <BaseButton @click="loadRates">Try again</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="rates.length === 0"
      icon="coins"
      title="No rates yet"
      description="Invoices can't be generated until the customer has a rate."
      compact
    >
      <BaseButton variant="primary" icon="plus" @click="handleAdd">Add a rate</BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">Type</th>
            <th scope="col" class="num">Price</th>
            <th scope="col">Billed</th>
            <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="rate in rates" :key="rate.id">
            <td class="primary-cell">{{ rateTypeName(rate.type) }}</td>
            <td class="num primary-cell">{{ formatMoney(rate.price.amount, rate.price.currency) }}</td>
            <td class="muted">per {{ unitFor(rate.type) }}</td>
            <td class="actions">
              <BaseButton size="sm" variant="ghost" icon="edit" @click="handleEdit(rate)">Edit</BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                title="Delete"
                :aria-label="`Delete ${rateTypeName(rate.type).toLowerCase()} rate`"
                @click="handleDelete(rate)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <BaseDialog
      :open="showForm"
      :title="editingRate ? 'Edit rate' : 'New rate'"
      size="sm"
      :close-on-overlay="false"
      @close="handleCloseForm"
    >
      <RateForm
        v-if="showForm"
        :customer-id="customerId"
        :rate-id="editingRate?.id"
        @success="handleFormSuccess"
        @cancel="handleCloseForm"
      />
    </BaseDialog>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRatesStore } from '@/stores/rates'
import RateForm from './RateForm.vue'
import { RateTypeNames, RateType } from '@/types'
import type { RateDto } from '@/types'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseDialog from '@/components/ui/BaseDialog.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { formatMoney } from '@/utils/format'

interface Props {
  customerId: number
}

const props = defineProps<Props>()
const ratesStore = useRatesStore()
const toast = useToast()
const { confirm } = useConfirm()

const showForm = ref(false)
const editingRate = ref<RateDto | null>(null)

const rates = computed(() => ratesStore.ratesByCustomer(props.customerId))
const loading = computed(() => ratesStore.loading)
const error = computed(() => ratesStore.error)

onMounted(() => {
  loadRates()
})

async function loadRates() {
  try {
    await ratesStore.fetchByCustomerId(props.customerId)
  } catch {
    // the store keeps the message; the empty state shows it
  }
}

function rateTypeName(type: RateType | string): string {
  if (typeof type === 'string') return type
  return RateTypeNames[type] || 'Unknown'
}

function unitFor(type: RateType | string): string {
  const name = rateTypeName(type)
  return name === 'Hourly' ? 'hour' : name === 'Monthly' ? 'month' : 'day'
}

function handleAdd() {
  editingRate.value = null
  showForm.value = true
}

function handleEdit(rate: RateDto) {
  editingRate.value = rate
  showForm.value = true
}

async function handleDelete(rate: RateDto) {
  const typeName = rateTypeName(rate.type).toLowerCase()
  if (!(await confirm({ title: 'Delete rate?', message: `This ${typeName} rate will be permanently deleted.`, confirmLabel: 'Delete', tone: 'danger' }))) {
    return
  }

  try {
    await ratesStore.remove(rate.id)
    toast.success('Rate deleted')
  } catch (error: any) {
    toast.failure('Failed to delete rate', error)
  }
}

function handleFormSuccess() {
  toast.success(editingRate.value ? 'Rate updated' : 'Rate added')
  showForm.value = false
  editingRate.value = null
  loadRates()
}

function handleCloseForm() {
  showForm.value = false
  editingRate.value = null
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
</style>
