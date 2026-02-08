import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import authService from '../services/auth.service';
import type { User } from 'oidc-client-ts';

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null);
  const isLoading = ref(true);
  const error = ref<string | null>(null);

  const isAuthenticated = computed(() => user.value !== null && !user.value.expired);
  
  const username = computed(() => 
    user.value?.profile.preferred_username || 
    user.value?.profile.name || 
    user.value?.profile.email || 
    'Unknown'
  );

  const email = computed(() => user.value?.profile.email || '');

  const roles = computed(() => 
    (user.value?.profile.realm_access as any)?.roles || []
  );

  const isAdmin = computed(() => roles.value.includes('admin'));

  async function initialize() {
    console.log('[Auth Store] Starting initialization...');
    isLoading.value = true;
    error.value = null;

    try {
      const currentUser = await authService.getUser();
      user.value = currentUser;
      console.log('[Auth Store] User:', currentUser ? 'authenticated' : 'not authenticated');
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to initialize auth';
      console.error('[Auth Store] Initialization error:', err);
    } finally {
      isLoading.value = false;
      console.log('[Auth Store] Initialization complete. isAuthenticated:', isAuthenticated.value);
    }
  }

  async function login() {
    console.log('[Auth Store] Login called - redirecting to Keycloak...');
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
