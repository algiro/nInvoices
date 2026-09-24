<template>
  <BaseDialog :open="state.open" :title="state.title" size="sm" @close="settle(false)">
    <p v-if="state.message" class="message">{{ state.message }}</p>
    <template #footer>
      <BaseButton @click="settle(false)">{{ state.cancelLabel ?? 'Cancel' }}</BaseButton>
      <BaseButton
        :variant="state.tone === 'danger' ? 'danger' : 'primary'"
        data-autofocus
        @click="settle(true)"
      >
        {{ state.confirmLabel ?? 'Confirm' }}
      </BaseButton>
    </template>
  </BaseDialog>
</template>

<script setup lang="ts">
import BaseDialog from './BaseDialog.vue'
import BaseButton from './BaseButton.vue'
import { useConfirm } from '@/composables/useConfirm'

const { state, settle } = useConfirm()
</script>

<style scoped>
.message {
  margin: 0;
  white-space: pre-line;
  line-height: 1.55;
}
</style>
