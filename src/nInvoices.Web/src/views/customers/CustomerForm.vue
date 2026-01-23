<template>
  <div class="customer-form-view">
    <CustomerFormComponent :customer-id="customerId" />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import CustomerFormComponent from '@/components/customers/CustomerForm.vue'

const route = useRoute()
const customerId = computed(() => {
  const id = route.params.id
  return id ? Number(id) : undefined
})
</script>

<style scoped>
.customer-form-view {
  min-height: calc(100vh - 4rem);
  background: #f9fafb;
}
</style>