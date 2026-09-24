<template>
  <CustomerFormComponent :key="customerId ?? 'new'" :customer-id="customerId" />
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
