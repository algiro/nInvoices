import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '../stores/auth';

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // Auth callback routes (no authentication required)
    {
      path: '/auth/callback',
      name: 'auth-callback',
      component: () => import('../views/AuthCallback.vue'),
    },
    {
      path: '/auth/silent-callback',
      name: 'auth-silent-callback',
      component: () => import('../views/SilentCallback.vue'),
    },
    // Main application routes (authentication required)
    {
      path: '/',
      component: () => import('../layouts/MainLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          name: 'dashboard',
          component: () => import('../views/Dashboard.vue'),
        },
        {
          path: 'customers',
          name: 'customers',
          component: () => import('../views/customers/CustomersList.vue'),
        },
        {
          path: 'customers/new',
          name: 'customer-create',
          component: () => import('../views/customers/CustomerForm.vue'),
        },
        {
          path: 'customers/:id',
          name: 'customer-details',
          component: () => import('../views/customers/CustomerDetails.vue'),
        },
        {
          path: 'customers/:id/edit',
          name: 'customer-edit',
          component: () => import('../views/customers/CustomerForm.vue'),
        },
        {
          path: 'invoices',
          name: 'invoices',
          component: () => import('../views/invoices/InvoicesList.vue'),
        },
        {
          path: 'invoices/new',
          name: 'invoice-generate',
          component: () => import('../views/invoices/InvoiceGenerate.vue'),
        },
        {
          path: 'invoices/:id',
          name: 'invoice-details',
          component: () => import('../views/invoices/InvoiceDetails.vue'),
        },
        {
          path: 'settings',
          name: 'settings',
          component: () => import('../views/settings/Settings.vue'),
        },
      ],
    },
  ],
});

// Navigation guard - check authentication before each route
router.beforeEach(async (to) => {
  console.log('[Router Guard] Navigating to:', to.path);
  const authStore = useAuthStore();
  
  // Initialize auth store if not already done
  if (authStore.isLoading) {
    console.log('[Router Guard] Auth is loading, waiting for initialization...');
    await authStore.initialize();
  }
  
  // Check if route requires authentication
  const requiresAuth = to.matched.some(record => record.meta.requiresAuth);
  console.log('[Router Guard] Route requires auth:', requiresAuth, 'isAuthenticated:', authStore.isAuthenticated);
  
  if (requiresAuth && !authStore.isAuthenticated) {
    console.log('[Router Guard] Not authenticated - redirecting to login');
    // Save the intended destination
    sessionStorage.setItem('returnUrl', to.fullPath);
    
    // Redirect to login
    await authStore.login();
    return;
  }
  
  console.log('[Router Guard] Navigation allowed');
  return true;
});

export default router;
