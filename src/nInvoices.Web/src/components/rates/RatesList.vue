<template>
  <section class="section">
    <header class="section-header">
      <div>
        <h3>{{ t('customers.tabs.rates') }}</h3>
        <p class="help">{{ t('rates.help') }}</p>
      </div>
      <BaseButton variant="primary" icon="plus" @click="handleAdd">{{ t('rates.new') }}</BaseButton>
    </header>

    <LoadingState v-if="loading && rates.length === 0" :label="t('rates.loading')" />

    <EmptyState v-else-if="error" icon="alert" :title="t('rates.loadFailed')" :description="error" compact>
      <BaseButton @click="loadRates">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="rates.length === 0"
      icon="coins"
      :title="t('rates.emptyTitle')"
      :description="t('rates.emptyDescription')"
      compact
    >
      <BaseButton variant="primary" icon="plus" @click="handleAdd">{{ t('invoiceWizard.customerStep.addRate') }}</BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">{{ t('customers.name') }}</th>
            <th scope="col">{{ t('common.type') }}</th>
            <th scope="col" class="num">{{ t('rates.price') }}</th>
            <th scope="col">{{ t('rates.billed') }}</th>
            <th scope="col" class="actions"><span class="sr-only">{{ t('common.actions') }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="rate in rates" :key="rate.id">
            <td class="primary-cell">{{ rate.name || '—' }}</td>
            <td>{{ rateTypeLabel(rate.type) }}</td>
            <td class="num primary-cell">{{ formatMoney(rate.price.amount, rate.price.currency) }}</td>
            <td class="muted">{{ rateUnit(rate.type) }}</td>
            <td class="actions">
              <BaseButton size="sm" variant="ghost" icon="edit" @click="handleEdit(rate)">{{ t('common.edit') }}</BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                :title="t('common.delete')"
                :aria-label="t('rates.deleteNamed', { type: rateTypeLabel(rate.type) })"
                @click="handleDelete(rate)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <BaseDialog
      :open="showForm"
      :title="editingRate ? t('rates.edit') : t('rates.new')"
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
import { rateUnit } from '@/utils/rates'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

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

function rateTypeLabel(type: RateType | string): string {
  const name = rateTypeName(type)
  return name === 'Hourly' || name === 'Monthly' || name === 'Daily' ? t(`rateType.${name}`) : name
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
  const type = rateTypeLabel(rate.type)
  const message = rate.name ? t('rates.deleteMessageNamed', { name: rate.name, type }) : t('rates.deleteMessage', { type })
  if (!(await confirm({ title: t('rates.deleteTitle'), message, confirmLabel: t('common.delete'), tone: 'danger' }))) {
    return
  }

  try {
    await ratesStore.remove(rate.id)
    toast.success(t('rates.deleted'))
  } catch (error) {
    toast.failure(t('rates.deleteFailed'), error)
  }
}

function handleFormSuccess() {
  toast.success(editingRate.value ? t('rates.updated') : t('rates.added'))
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
