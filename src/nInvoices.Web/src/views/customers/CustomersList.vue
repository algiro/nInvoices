<template>
  <div class="customers-page">
    <PageHeader title="Customers" :subtitle="subtitle">
      <template #actions>
        <BaseButton variant="primary" icon="plus" to="/customers/new">New customer</BaseButton>
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
          placeholder="Search by name, VAT number or city"
          aria-label="Search customers"
        />
      </div>
    </div>

    <LoadingState v-if="store.loading && store.customers.length === 0" label="Loading customers…" />

    <EmptyState v-else-if="store.error" icon="alert" title="Customers could not be loaded" :description="store.error">
      <BaseButton @click="loadData">Try again</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="store.customers.length === 0"
      icon="customers"
      title="No customers yet"
      description="Add a customer, then their rates and templates, before generating the first invoice."
    >
      <BaseButton variant="primary" icon="plus" to="/customers/new">New customer</BaseButton>
    </EmptyState>

    <EmptyState
      v-else-if="filteredCustomers.length === 0"
      icon="search"
      title="No customer matches your search"
      :description="`Nothing found for “${searchQuery}”.`"
      compact
    >
      <BaseButton @click="searchQuery = ''">Clear search</BaseButton>
    </EmptyState>

    <div v-else class="table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col">Customer</th>
            <th scope="col">Location</th>
            <th scope="col">Language</th>
            <th scope="col" class="actions"><span class="sr-only">Actions</span></th>
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
              <BaseButton size="sm" variant="ghost" icon="edit" :to="`/customers/${customer.id}/edit`">Edit</BaseButton>
              <BaseButton
                size="sm"
                variant="ghost-danger"
                icon="trash"
                icon-only
                title="Delete"
                :aria-label="`Delete ${customer.name}`"
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

const toast = useToast()
const { confirm } = useConfirm()

const router = useRouter()
const store = useCustomersStore()
const searchQuery = ref('')

const subtitle = computed(() => {
  const count = store.customers.length
  return count === 1 ? '1 customer' : `${count} customers`
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
  if (!(await confirm({ title: 'Delete customer?', message: `"${customer.name}" and all of their data will be permanently deleted.`, confirmLabel: 'Delete customer', tone: 'danger' }))) {
    return
  }

  try {
    await store.remove(customer.id)
    toast.success('Customer deleted')
  } catch (error: any) {
    toast.failure('Failed to delete customer', error)
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
