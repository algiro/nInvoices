import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import authService from '../services/auth.service';
import type { User } from 'oidc-client-ts';

const isAuthDisabled = import.meta.env.VITE_AUTH_DISABLED === 'true';

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null);
  const isLoading = ref(true);
  const error = ref<string | null>(null);

  const isAuthenticated = computed(() => {
    if (isAuthDisabled) return true;
    return user.value !== null && !user.value.expired;
  });

  const username = computed(() => {
    if (isAuthDisabled) return 'Developer';
    return user.value?.profile.preferred_username ||
      user.value?.profile.name ||
      user.value?.profile.email ||
      'Unknown';
  });

  const email = computed(() => {
    if (isAuthDisabled) return 'dev@localhost';
    return user.value?.profile.email || '';
  });

  const roles = computed(() => {
    if (isAuthDisabled) return ['user', 'admin'];
    return (user.value?.profile.realm_access as any)?.roles || [];
  });

  const isAdmin = computed(() => roles.value.includes('admin'));

  async function initialize() {
    if (isAuthDisabled) {
      console.log('[Auth Store] Auth disabled — using dev identity');
      isLoading.value = false;
      return;
    }

    isLoading.value = true;
    error.value = null;

    try {
      const currentUser = await authService.getUser();
      user.value = currentUser;
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to initialize auth';
      console.error('[Auth Store] Initialization error:', err);
    } finally {
      isLoading.value = false;
    }
  }

  async function login() {
    if (isAuthDisabled) return;

    error.value = null;
    try {
      await authService.login();
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Login failed';
      console.error('[Auth Store] Login error:', err);
      throw err;
    }
  }

  async function handleCallback() {
    isLoading.value = true;
    error.value = null;

    try {
      const authenticatedUser = await authService.handleCallback();
      user.value = authenticatedUser;
      return authenticatedUser;
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Callback handling failed';
      console.error('Callback error:', err);
      throw err;
    } finally {
      isLoading.value = false;
    }
  }

  async function logout() {
    if (isAuthDisabled) return;

    error.value = null;
    try {
      await authService.logout();
      user.value = null;
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Logout failed';
      console.error('Logout error:', err);
      throw err;
    }
  }

  async function getAccessToken(): Promise<string | null> {
    if (isAuthDisabled) return null;
    return await authService.getAccessToken();
  }

  async function refreshUser() {
    try {
      const currentUser = await authService.getUser();
      user.value = currentUser;
    } catch (err) {
      console.error('Failed to refresh user:', err);
    }
  }

  function hasRole(role: string): boolean {
    return roles.value.includes(role);
  }

  return {
    user,
    isLoading,
    error,
    isAuthenticated,
    username,
    email,
    roles,
    isAdmin,
    initialize,
    login,
    handleCallback,
    logout,
    getAccessToken,
    refreshUser,
    hasRole
  };
});
