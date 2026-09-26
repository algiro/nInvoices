<template>
  <div></div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import authService from '../services/auth.service';

onMounted(async () => {
  try {
    await authService.renewToken();
  } catch (error) {
    console.error('Silent token renewal failed:', error);
  }
});
</script>
