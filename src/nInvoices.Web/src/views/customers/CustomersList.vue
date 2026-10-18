<template>
  <div class="customers-page">
    <PageHeader :title="t('nav.customers')" :subtitle="subtitle">
      <template #actions>
        <BaseButton variant="primary" icon="plus" to="/customers/new">{{ t('customers.new') }}</BaseButton>
      </template>
    </PageHeader>

    <div class="toolbar">
      <div class="search">
        <AppIcon name="search" class="search-icon" />
        <input
          id="customer-search"
          v-model="searchQuery"
          type="search"
          class="control"
          :placeholder="t('customers.searchPlaceholder')"
          :aria-label="t('customers.search')"
        />
      </div>
    </div>

    <LoadingState v-if="store.loading && store.customers.length === 0" :label="t('customers.loading')" />

    <EmptyState v-else-if="store.error" icon="alert" :title="t('customers.loadFailed')" :description="store.error">
      <BaseButton @click="loadData">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="store.customers.length === 0"
      icon="customers"
      :title="t('customers.emptyTitle')"
      :description="t('customers.emptyDescription')"
    >
      <BaseButton variant="primary" icon="plus" to="/customers/new">{{ t('customers.new') }}</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="filteredCustomers.length === 0"
      icon="search"
      :title="t('customers.noMatch')"
      :description="t('common.nothingFoundFor', { query: searchQuery })"
      compact
    >
      <BaseButton @click="searchQuery = ''">{{ t('common.clearSearch') }}</BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">{{ t('customers.customer') }}</th>
            <th scope="col">{{ t('customers.location') }}</th>
            <th scope="col">{{ t('customers.language') }}</th>
            <th scope="col" class="actions"><span class="sr-only">{{ t('common.actions') }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="customer in filteredCustomers" :key="customer.id" class="clickable" @click="handleView(customer.id)">
            <td>
              <router-link :to="`/customers/${customer.id}`" class="primary-cell name-link" @click.stop>
                {{ customer.name }}
              </router-link>
              <span class="sub">{{ customer.fiscalId }}</span>
            </td>
            <td>{{ [customer.address.city, customer.address.country].filter(Boolean).join(', ') }}</td>
            <td class="muted">{{ localeLabel(customer.locale) }}</td>
            <td class="actions" @click.stop>
              <BaseButton size="sm" variant="ghost" icon="edit" :to="`/customers/${customer.id}/edit`">{{ t('common.edit') }}</BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                :title="t('common.delete')"
                :aria-label="t('common.deleteNamed', { name: customer.name })"
                @click="handleDelete(customer)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useCustomersStore } from '@/stores/customers'
import type { CustomerDto } from '@/types'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import PageHeader from '@/components/ui/PageHeader.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { localeLabel } from '@/utils/format'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const toast = useToast()
const { confirm } = useConfirm()

const router = useRouter()
const store = useCustomersStore()
const searchQuery = ref('')

const subtitle = computed(() => {
  return t('customers.count', store.customers.length)
})

const filteredCustomers = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  const list = [...store.customers].sort((a, b) => a.name.localeCompare(b.name))
  if (!query) return list
  return list.filter(customer =>
    customer.name.toLowerCase().includes(query) ||
    customer.fiscalId.toLowerCase().includes(query) ||
    customer.address.city.toLowerCase().includes(query) ||
    customer.address.country.toLowerCase().includes(query)
  )
})


onMounted(() => {
  loadData()
})

function loadData() {
  store.fetchAll()
}

function handleView(id: number) {
  router.push(`/customers/${id}`)
}

async function handleDelete(customer: CustomerDto) {
  if (!(await confirm({ title: t('customers.deleteTitle'), message: t('customers.deleteMessage', { name: customer.name }), confirmLabel: t('customers.deleteConfirm'), tone: 'danger' }))) {
    return
  }

  try {
    await store.remove(customer.id)
    toast.success(t('customers.deleted'))
  } catch (error) {
    toast.failure(t('customers.deleteFailed'), error)
  }
}
</script>

<style scoped>
.toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.search {
  position: relative;
  flex: 0 1 26rem;
}

.search-icon {
  position: absolute;
  left: 0.7rem;
  top: 50%;
  width: 1rem;
  height: 1rem;
  transform: translateY(-50%);
  color: var(--color-text-subtle);
  pointer-events: none;
}

.search .control {
  padding-left: 2.1rem;
}

.name-link {
  color: var(--color-text);
}

.name-link:hover {
  color: var(--color-primary);
}

</style>
