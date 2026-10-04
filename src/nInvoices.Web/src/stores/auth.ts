import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import authService from '../services/auth.service';
import type { User } from 'oidc-client-ts';

const isAuthDisabled = import.meta.env.VITE_AUTH_DISABLED === 'true';

/** The realm role the API requires on every endpoint. */
export const APP_USER_ROLE = 'user';

function realmRolesFromAccessToken(accessToken: string | undefined): string[] {
  const payload = accessToken?.split('.')[1];
  if (!payload) return [];
  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const claims = JSON.parse(atob(base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')));
    const roles = claims?.realm_access?.roles;
    return Array.isArray(roles) ? roles.filter((r: unknown): r is string => typeof r === 'string') : [];
  } catch {
    return [];
  }
}

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

  // Realm roles come from the access token, the same token the API checks
  // (the ID token / userinfo profile doesn't carry them by default).
  const roles = computed<string[]>(() => {
    if (isAuthDisabled) return ['user', 'admin'];
    return realmRolesFromAccessToken(user.value?.access_token);
  });

  const isAdmin = computed(() => roles.value.includes('admin'));

  // Signed in but not approved yet: an administrator grants the "user" role in Keycloak.
  const isApproved = computed(() => roles.value.includes(APP_USER_ROLE));

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

  /** Gets a fresh token from Keycloak, so a role granted meanwhile shows up without signing in again. */
  async function renewToken() {
    if (isAuthDisabled) return;
    user.value = await authService.renewToken();
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
    isApproved,
    initialize,
    login,
    handleCallback,
    logout,
    getAccessToken,
    refreshUser,
    renewToken,
    hasRole
  };
});
