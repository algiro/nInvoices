<template>
  <main class="account-deleted">
    <section class="card" aria-labelledby="account-deleted-title">
      <span class="brand-mark" aria-hidden="true">nI</span>
      <template v-if="signInKept">
        <h1 id="account-deleted-title">Your data has been deleted</h1>
        <p>
          Everything you had on this nInvoices server is gone. Your sign-in account was kept: signing in
          again gives you an empty workspace.
        </p>
        <p class="muted">You can delete the sign-in account later from Settings, or ask the administrator to.</p>
        <div class="actions">
          <BaseButton variant="primary" @click="authStore.logout()">Sign out</BaseButton>
          <BaseButton variant="ghost" to="/">Open nInvoices</BaseButton>
        </div>
      </template>
      <template v-else>
        <h1 id="account-deleted-title">Your account has been deleted</h1>
        <p>
          Your data and your sign-in account are gone from this nInvoices server. Copies in the server's
          backups can no longer be read.
        </p>
        <p class="muted">Thank you for using nInvoices. Keep the backup you downloaded: it opens without nInvoices too.</p>
      </template>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import BaseButton from '../components/ui/BaseButton.vue';

const route = useRoute();
const authStore = useAuthStore();
const signInKept = computed(() => route.query.signin === 'kept');
</script>

<style scoped>
.account-deleted {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  padding: 16px;
  box-sizing: border-box;
  background: var(--color-bg);
}

.card {
  max-width: 480px;
  width: 100%;
  padding: 32px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-text);
  box-shadow: var(--shadow-sm);
}

.brand-mark {
  display: inline-grid;
  place-items: center;
  width: 40px;
  height: 40px;
  border-radius: var(--radius-md);
  background: var(--color-primary);
  color: #fff;
  font-weight: 700;
}

h1 {
  margin: 20px 0 12px;
  font-size: 1.35rem;
  line-height: 1.3;
}

p {
  margin: 0 0 12px;
  line-height: 1.55;
}

.muted {
  color: var(--color-text-muted);
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 20px;
}
</style>
