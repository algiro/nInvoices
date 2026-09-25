import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import { clearPageTitle } from '../composables/usePageTitle';

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
          meta: { title: 'Dashboard', section: 'dashboard' },
          component: () => import('../views/Dashboard.vue'),
        },
        {
          path: 'customers',
          name: 'customers',
          meta: { title: 'Customers', section: 'customers' },
          component: () => import('../views/customers/CustomersList.vue'),
        },
        {
          path: 'customers/new',
          name: 'customer-create',
          meta: { title: 'New customer', section: 'customers', parent: { label: 'Customers', to: '/customers' } },
          component: () => import('../views/customers/CustomerForm.vue'),
        },
        {
          path: 'customers/:id',
          name: 'customer-details',
          meta: { title: 'Customer details', section: 'customers', parent: { label: 'Customers', to: '/customers' } },
          component: () => import('../views/customers/CustomerDetails.vue'),
        },
        {
          path: 'customers/:id/edit',
          name: 'customer-edit',
          meta: { title: 'Edit customer', section: 'customers', parent: { label: 'Customers', to: '/customers' } },
          component: () => import('../views/customers/CustomerForm.vue'),
        },
        {
          // templateId is a number, or 'new' for a template that hasn't been saved yet
          path: 'customers/:id/templates/invoice/:templateId',
          name: 'invoice-template-editor',
          meta: { title: 'Invoice template', section: 'customers', parent: { label: 'Customers', to: '/customers' }, kind: 'invoice' },
          component: () => import('../views/templates/TemplateEditor.vue'),
        },
        {
          path: 'customers/:id/templates/monthly-report/:templateId',
          name: 'monthly-report-template-editor',
          meta: { title: 'Monthly report template', section: 'customers', parent: { label: 'Customers', to: '/customers' }, kind: 'monthly-report' },
          component: () => import('../views/templates/TemplateEditor.vue'),
        },
        {
          path: 'customers/:id/templates/email/:templateId',
          name: 'email-template-editor',
          meta: { title: 'Email template', section: 'customers', parent: { label: 'Customers', to: '/customers' }, kind: 'email' },
          component: () => import('../views/templates/TemplateEditor.vue'),
        },
        {
          path: 'invoices',
          name: 'invoices',
          meta: { title: 'Invoices', section: 'invoices' },
          component: () => import('../views/invoices/InvoicesList.vue'),
        },
        {
          path: 'invoices/new',
          name: 'invoice-generate',
          meta: { title: 'New invoice', section: 'invoices', parent: { label: 'Invoices', to: '/invoices' } },
          component: () => import('../views/invoices/InvoiceGenerate.vue'),
        },
        {
          path: 'invoices/:id',
          name: 'invoice-details',
          meta: { title: 'Invoice details', section: 'invoices', parent: { label: 'Invoices', to: '/invoices' } },
          component: () => import('../views/invoices/InvoiceDetails.vue'),
        },
        {
          path: 'settings',
          name: 'settings',
          meta: { title: 'Settings', section: 'settings' },
          component: () => import('../views/settings/Settings.vue'),
        },
      ],
    },
  ],
});

// Navigation guard - check authentication before each route
router.beforeEach(async (to) => {
  const authStore = useAuthStore();
  
  // Initialize auth store if not already done
  if (authStore.isLoading) {
    await authStore.initialize();
  }
  
  // Check if route requires authentication
  const requiresAuth = to.matched.some(record => record.meta.requiresAuth);
  
  if (requiresAuth && !authStore.isAuthenticated) {
    // Save the intended destination
    sessionStorage.setItem('returnUrl', to.fullPath);
    
    // Redirect to login
    await authStore.login();
    return;
  }
  
  return true;
});

// Browser tab title follows the page title
router.afterEach((to, from) => {
  // Query-only changes (e.g. switching tabs) keep the page's own title
  if (to.path === from.path && from.matched.length > 0) return;
  clearPageTitle();
  const title = to.meta.title as string | undefined;
  document.title = title ? `${title} · nInvoices` : 'nInvoices';
});

export default router;
