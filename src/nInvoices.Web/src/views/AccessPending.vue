<template>
  <main class="access-pending">
    <section class="card" aria-labelledby="access-pending-title">
      <span class="brand-mark" aria-hidden="true">nI</span>
      <h1 id="access-pending-title">Your account is waiting for approval</h1>
      <p>
        You're signed in as <strong>{{ authStore.email || authStore.username }}</strong>, but this
        nInvoices server only lets in accounts its administrator has approved.
      </p>
      <p v-if="administratorNotified" class="muted" role="status">
        The administrator has been notified. Once they approve your account, check again.
      </p>
      <p v-else class="muted">
        Ask the administrator to approve your account, then check again. Nothing you do here is
        saved until then.
      </p>
      <p v-if="stillPending" class="status" role="status">Not approved yet.</p>
      <div class="actions">
        <BaseButton variant="primary" :loading="checking" @click="checkAgain">Check again</BaseButton>
        <BaseButton variant="ghost" @click="authStore.logout()">Sign out</BaseButton>
      </div>
    </section>
  </main>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import BaseButton from '../components/ui/BaseButton.vue';
import { accountApi } from '../api/account';

const router = useRouter();
const authStore = useAuthStore();
const checking = ref(false);
const stillPending = ref(false);
const administratorNotified = ref(false);

// Tells the administrator someone is waiting (once per account; later visits only read the state)
onMounted(async () => {
  try {
    administratorNotified.value = (await accountApi.requestAccess()).administratorNotified;
  } catch (err) {
    console.error('[Access pending] Could not request access:', err);
  }
});

async function checkAgain() {
  checking.value = true;
  stillPending.value = false;
  try {
    await authStore.renewToken();
  } catch (err) {
    console.error('[Access pending] Token renewal failed:', err);
  } finally {
    checking.value = false;
  }

  if (authStore.isApproved) {
    await router.replace('/');
  } else {
    stillPending.value = true;
  }
}
</script>

<style scoped>
.access-pending {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  padding: 16px;
  box-sizing: border-box;
  background: var(--color-bg);
}

.card {
  max-width: 460px;
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
  overflow-wrap: anywhere;
}

.muted {
  color: var(--color-text-muted);
}

.status {
  color: var(--color-text-secondary);
  font-weight: 600;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 20px;
}
</style>
