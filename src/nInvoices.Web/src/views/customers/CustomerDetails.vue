<template>
  <div class="customer-details">
    <LoadingState v-if="loading && !customer" :label="t('customers.loadingOne')" />

    <EmptyState v-else-if="error && !customer" icon="alert" :title="t('customers.loadOneFailed')" :description="error">
      <BaseButton @click="loadData">{{ t('common.tryAgain') }}</BaseButton>
      <BaseButton variant="ghost" to="/customers">{{ t('customers.backToList') }}</BaseButton>
    </EmptyState>

    <template v-else-if="customer">
      <PageHeader :title="customer.name">
        <template #subtitle>
          <span class="meta">
            <span>{{ customer.fiscalId }}</span>
            <span class="dot" aria-hidden="true">·</span>
            <span><AppIcon name="mapPin" class="meta-icon" />{{ [customer.address.city, customer.address.country].filter(Boolean).join(', ') }}</span>
            <span class="dot" aria-hidden="true">·</span>
            <span>{{ localeLabel(customer.locale) }}</span>
          </span>
        </template>
        <template #actions>
          <BaseButton icon="edit" :to="`/customers/${customerId}/edit`">{{ t('common.edit') }}</BaseButton>
          <BaseButton variant="ghost-danger" icon="trash" @click="handleDelete">{{ t('common.delete') }}</BaseButton>
          <BaseButton variant="primary" icon="plus" :to="{ path: '/invoices/new', query: { customerId } }">{{ t('layout.newInvoice') }}</BaseButton>
        </template>
      </PageHeader>

      <BaseTabs :model-value="activeTab" :tabs="tabs" :label="t('customers.sections')" @update:model-value="selectTab" />

      <div class="tab-body" role="tabpanel" :aria-labelledby="`tab-${activeTab}`">
        <div v-if="activeTab === 'overview'" class="overview">
          <BasePanel :title="t('customers.details')">
            <dl class="facts">
              <div><dt>{{ t('customers.name') }}</dt><dd>{{ customer.name }}</dd></div>
              <div><dt>{{ t('customers.fiscalId') }}</dt><dd>{{ customer.fiscalId }}</dd></div>
              <div>
                <dt>{{ t('customers.documentLanguage') }}</dt>
                <dd>
                  {{ localeLabel(customer.locale) }}
                  <span v-if="customer.locale" class="muted">({{ customer.locale }})</span>
                  <router-link v-else :to="`/customers/${customerId}/edit`" class="set-link">{{ t('customers.chooseLanguage') }}</router-link>
                </dd>
              </div>
              <div>
                <dt>{{ t('customers.emailsTo') }}</dt>
                <dd>
                  <template v-if="customer.email">
                    {{ customer.email }}
                    <span v-if="customer.ccEmails" class="muted">{{ t('customers.cc', { emails: customer.ccEmails }) }}</span>
                  </template>
                  <router-link v-else :to="`/customers/${customerId}/edit`" class="set-link">{{ t('customers.addEmail') }}</router-link>
                </dd>
              </div>
              <div><dt>{{ t('customers.since') }}</dt><dd>{{ formatDate(customer.createdAt) }}</dd></div>
            </dl>
          </BasePanel>

          <BasePanel :title="t('customers.billingAddress')">
            <address class="address">
              {{ customer.address.street }} {{ customer.address.houseNumber }}<br>
              {{ customer.address.zipCode }} {{ customer.address.city }}<br>
              <template v-if="customer.address.state">{{ customer.address.state }}<br></template>
              {{ customer.address.country }}
            </address>
          </BasePanel>

          <BasePanel :title="t('customers.setup')" :description="t('customers.setupDescription')" class="setup">
            <ul class="setup-links">
              <li><button type="button" @click="selectTab('rates')"><AppIcon name="coins" />{{ t('customers.tabs.rates') }}<span>{{ t('customers.setupRates') }}</span></button></li>
              <li><button type="button" @click="selectTab('taxes')"><AppIcon name="percent" />{{ t('customers.tabs.taxes') }}<span>{{ t('customers.setupTaxes') }}</span></button></li>
              <li><button type="button" @click="selectTab('projects')"><AppIcon name="folder" />{{ t('customers.tabs.projects') }}<span>{{ t('customers.setupProjects') }}</span></button></li>
              <li><button type="button" @click="selectTab('templates')"><AppIcon name="template" />{{ t('customers.tabs.templates') }}<span>{{ t('customers.setupTemplates') }}</span></button></li>
              <li><button type="button" @click="selectTab('monthly-reports')"><AppIcon name="calendar" />{{ t('customers.tabs.monthlyReports') }}<span>{{ t('customers.setupMonthlyReports') }}</span></button></li>
              <li><button type="button" @click="selectTab('emails')"><AppIcon name="send" />{{ t('customers.tabs.emails') }}<span>{{ t('customers.setupEmails') }}</span></button></li>
            </ul>
          </BasePanel>
        </div>

        <BasePanel v-else>
          <RatesList v-if="activeTab === 'rates'" :customer-id="customerId" />
          <ProjectsList v-else-if="activeTab === 'projects'" :customer-id="customerId" />
          <TaxesList v-else-if="activeTab === 'taxes'" :customer-id="customerId" />
          <TemplateListPanel v-else-if="activeTab === 'templates'" kind="invoice" :customer-id="customerId" />
          <TemplateListPanel v-else-if="activeTab === 'monthly-reports'" kind="monthly-report" :customer-id="customerId" />
          <TemplateListPanel v-else-if="activeTab === 'emails'" kind="email" :customer-id="customerId" />
          <CustomerInvoices v-else-if="activeTab === 'invoices'" :customer-id="customerId" />
        </BasePanel>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useCustomersStore } from '@/stores/customers'
import RatesList from '@/components/rates/RatesList.vue'
import ProjectsList from '@/components/projects/ProjectsList.vue'
import TaxesList from '@/components/taxes/TaxesList.vue'
import TemplateListPanel from '@/components/templates/TemplateListPanel.vue'
import CustomerInvoices from '@/components/customers/CustomerInvoices.vue'
import PageHeader from '@/components/ui/PageHeader.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseTabs from '@/components/ui/BaseTabs.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { useConfirm } from '@/composables/useConfirm'
import { useToast, errorMessage } from '@/composables/useToast'
import { setPageTitle } from '@/composables/usePageTitle'
import { formatDate, localeLabel } from '@/utils/format'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const { confirm } = useConfirm()
const toast = useToast()

const route = useRoute()
const router = useRouter()
const customersStore = useCustomersStore()

const customerId = computed(() => Number(route.params.id))
// Only show the store's selection when it is this customer (it may still hold the previous one)
const customer = computed(() =>
  customersStore.selectedCustomer?.id === customerId.value ? customersStore.selectedCustomer : null
)
const loading = ref(false)
const error = ref<string | null>(null)

const tabs = computed(() => [
  { id: 'overview', label: t('customers.tabs.overview') },
  { id: 'rates', label: t('customers.tabs.rates') },
  { id: 'taxes', label: t('customers.tabs.taxes') },
  { id: 'projects', label: t('customers.tabs.projects') },
  { id: 'templates', label: t('customers.tabs.templates') },
  { id: 'monthly-reports', label: t('customers.tabs.monthlyReports') },
  { id: 'emails', label: t('customers.tabs.emails') },
  { id: 'invoices', label: t('customers.tabs.invoices') }
])

// The open tab lives in the URL (?tab=…) so links such as the template editor's "back" land on it
const activeTab = computed(() => {
  const tab = route.query.tab
  return typeof tab === 'string' && tabs.value.some(item => item.id === tab) ? tab : 'overview'
})

function selectTab(id: string) {
  router.replace({ query: { ...route.query, tab: id === 'overview' ? undefined : id } })
}


onMounted(() => {
  loadData()
})

watch(customerId, () => loadData())
watch(customer, value => setPageTitle(value?.name), { immediate: true })

async function loadData() {
  try {
    loading.value = true
    error.value = null
    await customersStore.fetchById(customerId.value)
  } catch (err) {
    error.value = errorMessage(err, t('customers.loadOneFailed'))
  } finally {
    loading.value = false
  }
}

async function handleDelete() {
  if (!(await confirm({ title: t('customers.deleteTitle'), message: t('customers.deleteMessage', { name: customer.value?.name ?? '' }), confirmLabel: t('customers.deleteConfirm'), tone: 'danger' }))) {
    return
  }

  try {
    await customersStore.remove(customerId.value)
    toast.success(t('customers.deleted'))
    router.push('/customers')
  } catch (err) {
    toast.failure(t('customers.deleteFailed'), err)
  }
}
</script>

<style scoped>
.meta {
  display: inline-flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.2rem 0.5rem;
}

.meta span {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
}

.meta-icon {
  width: 0.95rem;
  height: 0.95rem;
  color: var(--color-text-subtle);
}

.dot {
  color: var(--color-text-subtle);
}

.tab-body {
  margin-top: 1.25rem;
}

.overview {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
  align-items: start;
}

.overview .setup {
  grid-column: 1 / -1;
}

@media (max-width: 800px) {
  .overview {
    grid-template-columns: minmax(0, 1fr);
  }
}

.facts {
  display: grid;
  gap: 0.85rem;
  margin: 0;
}

.facts dt {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.facts dd {
  margin: 0.1rem 0 0;
  font-weight: 500;
  color: var(--color-text);
  overflow-wrap: anywhere;
}

.set-link {
  margin-left: 0.35rem;
  font-weight: 500;
}

.address {
  font-style: normal;
  line-height: 1.6;
  color: var(--color-text);
}

.setup-links {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(13rem, 1fr));
  gap: 0.5rem;
  margin: 0;
  padding: 0;
  list-style: none;
}

.setup-links button {
  width: 100%;
  height: 100%;
  display: grid;
  grid-template-columns: auto 1fr;
  grid-template-rows: auto auto;
  column-gap: 0.6rem;
  align-items: center;
  padding: 0.7rem 0.8rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font-weight: 600;
  text-align: left;
}

.setup-links button:hover {
  border-color: var(--color-primary-line);
  background: var(--color-primary-soft);
  color: var(--color-text);
}

.setup-links .app-icon {
  grid-row: 1 / 3;
  color: var(--color-primary);
}

.setup-links span {
  font-size: var(--text-sm);
  font-weight: 400;
  color: var(--color-text-muted);
}
</style>
