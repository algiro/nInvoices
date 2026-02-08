<template>
  <div class="user-menu" v-if="authStore.isAuthenticated">
    <div class="user-info">
      <div class="user-avatar">
        {{ userInitials }}
      </div>
      <div class="user-details">
        <div class="user-name">{{ authStore.username }}</div>
        <div class="user-email">{{ authStore.email }}</div>
        <div class="user-roles" v-if="authStore.roles.length > 0">
          <span v-for="role in authStore.roles" :key="role" class="role-badge">
            {{ role }}
          </span>
        </div>
      </div>
    </div>
    <button @click="handleLogout" class="logout-button" :disabled="isLoggingOut">
      <span v-if="!isLoggingOut">Logout</span>
      <span v-else>Logging out...</span>
    </button>
  </div>
  <div class="login-prompt" v-else>
    <button @click="handleLogin" class="login-button">
      Login
    </button>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAuthStore } from '../stores/auth';

const authStore = useAuthStore();
const isLoggingOut = ref(false);

const userInitials = computed(() => {
  const name = authStore.username || authStore.email || 'U';
  const parts = name.split(' ');
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }
  return name.substring(0, 2).toUpperCase();
});

async function handleLogin() {
  try {
    await authStore.login();
  } catch (error) {
    console.error('Login failed:', error);
  }
}

async function handleLogout() {
  isLoggingOut.value = true;
  try {
    await authStore.logout();
  } catch (error) {
    console.error('Logout failed:', error);
  } finally {
    isLoggingOut.value = false;
  }
}
</script>

<style scoped>
.user-menu {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0.5rem;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.user-info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.user-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 600;
  font-size: 0.9rem;
}

.user-details {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.user-name {
  font-weight: 600;
  font-size: 0.95rem;
  color: #2d3748;
}

.user-email {
  font-size: 0.85rem;
  color: #718096;
}

.user-roles {
  display: flex;
  gap: 0.25rem;
  flex-wrap: wrap;
  margin-top: 0.25rem;
}

.role-badge {
  font-size: 0.75rem;
  padding: 0.125rem 0.5rem;
  background: #e6f2ff;
  color: #0066cc;
  border-radius: 12px;
  font-weight: 500;
}

.logout-button,
.login-button {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 6px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.logout-button {
  background: #f56565;
  color: white;
}

.logout-button:hover:not(:disabled) {
  background: #e53e3e;
}

.logout-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.login-button {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 0.75rem 1.5rem;
  font-size: 1rem;
}

.login-button:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 8px rgba(102, 126, 234, 0.3);
}

.login-prompt {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
}

@media (max-width: 768px) {
  .user-menu {
    flex-direction: column;
    gap: 0.75rem;
  }

  .user-info {
    width: 100%;
  }

  .logout-button {
    width: 100%;
  }
}
</style>
