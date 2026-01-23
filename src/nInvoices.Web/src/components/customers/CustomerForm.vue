<template>
  <div class="customer-form">
    <h2 class="text-2xl font-bold mb-6">{{ isEditMode ? 'Edit Customer' : 'New Customer' }}</h2>

    <form @submit.prevent="handleSubmit" class="space-y-6">
      <div class="form-section">
        <h3 class="text-lg font-semibold mb-4">Basic Information</h3>
        
        <div class="form-group">
          <label for="name" class="block text-sm font-medium text-gray-700 mb-1">
            Customer Name <span class="text-red-500">*</span>
          </label>
          <input
            id="name"
            v-model="form.name"
            type="text"
            required
            class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            :class="{ 'border-red-500': errors.name }"
          />
          <p v-if="errors.name" class="text-red-500 text-sm mt-1">{{ errors.name }}</p>
        </div>

        <div class="form-group">
          <label for="fiscalId" class="block text-sm font-medium text-gray-700 mb-1">
            Fiscal ID / VAT <span class="text-red-500">*</span>
          </label>
          <input
            id="fiscalId"
            v-model="form.fiscalId"
            type="text"
            required
            class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            :class="{ 'border-red-500': errors.fiscalId }"
          />
          <p v-if="errors.fiscalId" class="text-red-500 text-sm mt-1">{{ errors.fiscalId }}</p>
        </div>
      </div>

      <div class="form-section">
        <h3 class="text-lg font-semibold mb-4">Address</h3>
        
        <div class="grid grid-cols-2 gap-4">
          <div class="form-group">
            <label for="street" class="block text-sm font-medium text-gray-700 mb-1">
              Street <span class="text-red-500">*</span>
            </label>
            <input
              id="street"
              v-model="form.address.street"
              type="text"
              required
              class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div class="form-group">
            <label for="houseNumber" class="block text-sm font-medium text-gray-700 mb-1">
              House Number <span class="text-red-500">*</span>
            </label>
            <input
              id="houseNumber"
              v-model="form.address.houseNumber"
              type="text"
              required
              class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>

        <div class="grid grid-cols-2 gap-4">
          <div class="form-group">
            <label for="city" class="block text-sm font-medium text-gray-700 mb-1">
              City <span class="text-red-500">*</span>
            </label>
            <input
              id="city"
              v-model="form.address.city"
              type="text"
              required
              class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div class="form-group">
            <label for="zipCode" class="block text-sm font-medium text-gray-700 mb-1">
              ZIP Code <span class="text-red-500">*</span>
            </label>
            <input
              id="zipCode"
              v-model="form.address.zipCode"
              type="text"
              required
              class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>

        <div class="grid grid-cols-2 gap-4">
          <div class="form-group">
            <label for="country" class="block text-sm font-medium text-gray-700 mb-1">
              Country <span class="text-red-500">*</span>
            </label>
            <input
              id="country"
              v-model="form.address.country"
              type="text"
              required
              class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div class="form-group">
            <label for="state" class="block text-sm font-medium text-gray-700 mb-1">
              State / Province
            </label>
            <input
              id="state"
              v-model="form.address.state"
              type="text"
              class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>
      </div>

      <div class="flex justify-end gap-3 pt-4 border-t">
        <button
          type="button"
          @click="handleCancel"
          class="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50 transition"
          :disabled="loading"
        >
          Cancel
        </button>
        <button
          type="submit"
          class="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition disabled:opacity-50"
          :disabled="loading"
        >
          {{ loading ? 'Saving...' : 'Save Customer' }}
        </button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useCustomersStore } from '@/stores/customers'
import type { CustomerDto, CreateCustomerDto, UpdateCustomerDto } from '@/types'

interface Props {
  customerId?: number
}

const props = defineProps<Props>()
const router = useRouter()
const customersStore = useCustomersStore()

const isEditMode = computed(() => !!props.customerId)
const loading = ref(false)

const form = reactive<CreateCustomerDto | UpdateCustomerDto>({
  name: '',
  fiscalId: '',
  address: {
    street: '',
    houseNumber: '',
    city: '',
    zipCode: '',
    country: '',
    state: null
  }
})

const errors = reactive<Record<string, string>>({})

onMounted(async () => {
  if (isEditMode.value && props.customerId) {
    await loadCustomer(props.customerId)
  }
})

async function loadCustomer(id: number) {
  try {
    loading.value = true
    const customer = await customersStore.fetchById(id)
    if (customer) {
      form.name = customer.name
      form.fiscalId = customer.fiscalId
      form.address = { ...customer.address }
    }
  } catch (error) {
    console.error('Failed to load customer:', error)
  } finally {
    loading.value = false
  }
}

function validateForm(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])

  if (!form.name.trim()) {
    errors.name = 'Name is required'
  }

  if (!form.fiscalId.trim()) {
    errors.fiscalId = 'Fiscal ID is required'
  }

  return Object.keys(errors).length === 0
}

async function handleSubmit() {
  if (!validateForm()) {
    return
  }

  try {
    loading.value = true

    if (isEditMode.value && props.customerId) {
      await customersStore.update(props.customerId, form as UpdateCustomerDto)
    } else {
      await customersStore.create(form as CreateCustomerDto)
    }

    router.push('/customers')
  } catch (error: any) {
    console.error('Failed to save customer:', error)
    errors.name = error.message || 'Failed to save customer'
  } finally {
    loading.value = false
  }
}

function handleCancel() {
  router.push('/customers')
}
</script>

<style scoped>
.customer-form {
  max-width: 800px;
  margin: 0 auto;
  padding: 2rem;
}

.form-section {
  background: white;
  padding: 1.5rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.form-group {
  margin-bottom: 1rem;
}

.form-group:last-child {
  margin-bottom: 0;
}
</style>