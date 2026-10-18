<template>
  <main class="account-deleted">
    <section class="card" aria-labelledby="account-deleted-title">
      <span class="brand-mark" aria-hidden="true">nI</span>
      <template v-if="signInKept">
        <h1 id="account-deleted-title">{{ t('accountDeleted.dataDeletedTitle') }}</h1>
        <p>{{ t('accountDeleted.dataDeleted') }}</p>
        <p class="muted">{{ t('accountDeleted.deleteSignInLater') }}</p>
        <div class="actions">
          <BaseButton variant="primary" @click="authStore.logout()">{{ t('layout.signOut') }}</BaseButton>
          <BaseButton variant="ghost" to="/">{{ t('accountDeleted.openApp') }}</BaseButton>
        </div>
      </template>
      <template v-else>
        <h1 id="account-deleted-title">{{ t('accountDeleted.accountDeletedTitle') }}</h1>
        <p>{{ t('accountDeleted.accountDeleted') }}</p>
        <p class="muted">{{ t('accountDeleted.thankYou') }}</p>
      </template>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import BaseButton from '../components/ui/BaseButton.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

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
