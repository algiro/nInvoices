<template>
  <div class="customer-form">
    <h2 class="form-title">{{ isEditMode ? 'Edit Customer' : 'New Customer' }}</h2>

    <form @submit.prevent="handleSubmit">
      <div class="form-section">
        <h3 class="section-title">Basic Information</h3>
        
        <div class="form-group">
          <label for="name" class="form-label">
            Customer Name <span class="required">*</span>
          </label>
          <input
            id="name"
            v-model="form.name"
            type="text"
            required
            class="form-input"
            :class="{ 'input-error': errors.name }"
          />
          <p v-if="errors.name" class="error-text">{{ errors.name }}</p>
        </div>

        <div class="form-group">
          <label for="fiscalId" class="form-label">
            Fiscal ID / VAT <span class="required">*</span>
          </label>
          <input
            id="fiscalId"
            v-model="form.fiscalId"
            type="text"
            required
            class="form-input"
            :class="{ 'input-error': errors.fiscalId }"
          />
          <p v-if="errors.fiscalId" class="error-text">{{ errors.fiscalId }}</p>
        </div>
      </div>

      <div class="form-section">
        <h3 class="section-title">Address</h3>
        
        <div class="form-row">
          <div class="form-group col-3">
            <label for="street" class="form-label">
              Street <span class="required">*</span>
            </label>
            <input
              id="street"
              v-model="form.address.street"
              type="text"
              required
              class="form-input"
            />
          </div>

          <div class="form-group col-1">
            <label for="houseNumber" class="form-label">
              House Number <span class="required">*</span>
            </label>
            <input
              id="houseNumber"
              v-model="form.address.houseNumber"
              type="text"
              required
              class="form-input"
            />
          </div>
        </div>

        <div class="form-row">
          <div class="form-group col-2">
            <label for="city" class="form-label">
              City <span class="required">*</span>
            </label>
            <input
              id="city"
              v-model="form.address.city"
              type="text"
              required
              class="form-input"
            />
          </div>

          <div class="form-group col-1">
            <label for="zipCode" class="form-label">
              ZIP Code <span class="required">*</span>
            </label>
            <input
              id="zipCode"
              v-model="form.address.zipCode"
              type="text"
              required
              class="form-input"
            />
          </div>
        </div>

        <div class="form-row">
          <div class="form-group col-2">
            <label for="country" class="form-label">
              Country <span class="required">*</span>
            </label>
            <input
              id="country"
              v-model="form.address.country"
              type="text"
              required
              class="form-input"
            />
          </div>

          <div class="form-group col-2">
            <label for="state" class="form-label">
              State / Province
            </label>
            <input
              id="state"
              v-model="form.address.state"
              type="text"
              class="form-input"
            />
          </div>
        </div>
      </div>

      <div class="form-actions">
        <button
          type="button"
          @click="handleCancel"
          class="btn btn-cancel"
          :disabled="loading"
        >
          Cancel
        </button>
        <button
          type="submit"
          class="btn btn-primary"
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

.form-title {
  font-size: 1.5rem;
  font-weight: 700;
  margin-bottom: 1.5rem;
  color: #1f2937;
}

.form-section {
  background: white;
  padding: 1.5rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  margin-bottom: 1.5rem;
}

.section-title {
  font-size: 1.125rem;
  font-weight: 600;
  margin-bottom: 1.25rem;
  color: #374151;
}

.form-row {
  display: flex;
  gap: 1rem;
  margin-bottom: 0;
}

.form-group {
  margin-bottom: 1rem;
  flex: 1;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-row .form-group {
  margin-bottom: 1rem;
}

.col-1 { flex: 1; }
.col-2 { flex: 2; }
.col-3 { flex: 3; }

.form-label {
  display: block;
  font-size: 0.875rem;
  font-weight: 500;
  color: #374151;
  margin-bottom: 0.375rem;
}

.required {
  color: #ef4444;
}

.form-input {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: 1px solid #d1d5db;
  border-radius: 0.375rem;
  font-size: 0.875rem;
  color: #1f2937;
  background: white;
  transition: border-color 0.15s, box-shadow 0.15s;
  box-sizing: border-box;
}

.form-input:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.form-input.input-error {
  border-color: #ef4444;
}

.error-text {
  font-size: 0.8rem;
  color: #ef4444;
  margin-top: 0.25rem;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding-top: 1.25rem;
  border-top: 1px solid #e5e7eb;
}

.btn {
  padding: 0.5rem 1.25rem;
  border-radius: 0.375rem;
  font-weight: 500;
  font-size: 0.875rem;
  cursor: pointer;
  transition: all 0.15s;
  border: none;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-cancel {
  background: white;
  color: #374151;
  border: 1px solid #d1d5db;
}

.btn-cancel:hover:not(:disabled) {
  background: #f9fafb;
}

.btn-primary {
  background: #2563eb;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #1d4ed8;
}

@media (max-width: 600px) {
  .form-row {
    flex-direction: column;
    gap: 0;
  }
  
  .col-1, .col-2, .col-3 {
    flex: none;
  }
}
</style>