<template>
  <div class="auth-callback">
    <div class="loading-spinner">
      <div class="spinner"></div>
      <p>{{ message }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import authService from '../services/auth.service';

const router = useRouter();
const authStore = useAuthStore();
const message = ref('Completing authentication...');

onMounted(async () => {
  // Back from Keycloak's "Delete account" page: the sign-in account is gone, unless the user cancelled
  if (authService.takeDeletingAccountFlag()) {
    const cancelled = new URLSearchParams(window.location.search).get('kc_action_status') === 'cancelled';
    if (cancelled) {
      try {
        await authStore.handleCallback();
      } catch (error) {
        console.error('Callback after cancelled account deletion:', error);
      }
    } else {
      await authService.forgetUser();
    }
    await router.replace({ name: 'account-deleted', query: cancelled ? { signin: 'kept' } : {} });
    return;
  }

  try {
    await authStore.handleCallback();
    message.value = 'Authentication successful! Redirecting...';
    
    // Redirect to home or intended route
    const returnUrl = sessionStorage.getItem('returnUrl') || '/';
    sessionStorage.removeItem('returnUrl');
    
    setTimeout(() => {
      router.push(returnUrl);
    }, 1000);
  } catch (error) {
    console.error('Authentication callback error:', error);
    message.value = 'Authentication failed. Redirecting to login...';
    
    setTimeout(() => {
      router.push('/');
    }, 2000);
  }
});
</script>

<style scoped>
.auth-callback {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.loading-spinner {
  text-align: center;
  color: white;
}

.spinner {
  border: 4px solid rgba(255, 255, 255, 0.3);
  border-top: 4px solid white;
  border-radius: 50%;
  width: 50px;
  height: 50px;
  animation: spin 1s linear infinite;
  margin: 0 auto 20px;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

p {
  font-size: 1.2rem;
  margin: 0;
}
</style>
