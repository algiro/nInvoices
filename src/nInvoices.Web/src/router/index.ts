import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import { clearPageTitle } from '../composables/usePageTitle';
import { t } from '@/i18n';

// meta.title and meta.parent.label are message keys (routes.*), translated where they are shown
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
    {
      // Signed in, but an administrator hasn't approved the account yet
      path: '/access-pending',
      name: 'access-pending',
      meta: { title: 'routes.accessPending' },
      component: () => import('../views/AccessPending.vue'),
    },
    {
      // After Settings > Delete account (no sign-in needed: the account may be gone)
      path: '/account-deleted',
      name: 'account-deleted',
      meta: { title: 'routes.accountDeleted' },
      component: () => import('../views/AccountDeleted.vue'),
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
          meta: { title: 'routes.dashboard', section: 'dashboard' },
          component: () => import('../views/Dashboard.vue'),
        },
        {
          path: 'customers',
          name: 'customers',
          meta: { title: 'routes.customers', section: 'customers' },
          component: () => import('../views/customers/CustomersList.vue'),
        },
        {
          path: 'customers/new',
          name: 'customer-create',
          meta: { title: 'routes.newCustomer', section: 'customers', parent: { label: 'routes.customers', to: '/customers' } },
          component: () => import('../views/customers/CustomerForm.vue'),
        },
        {
          path: 'customers/:id',
          name: 'customer-details',
          meta: { title: 'routes.customerDetails', section: 'customers', parent: { label: 'routes.customers', to: '/customers' } },
          component: () => import('../views/customers/CustomerDetails.vue'),
        },
        {
          path: 'customers/:id/edit',
          name: 'customer-edit',
          meta: { title: 'routes.editCustomer', section: 'customers', parent: { label: 'routes.customers', to: '/customers' } },
          component: () => import('../views/customers/CustomerForm.vue'),
        },
        {
          // templateId is a number, or 'new' for a template that hasn't been saved yet
          path: 'customers/:id/templates/invoice/:templateId',
          name: 'invoice-template-editor',
          meta: { title: 'routes.invoiceTemplate', section: 'customers', parent: { label: 'routes.customers', to: '/customers' }, kind: 'invoice' },
          component: () => import('../views/templates/TemplateEditor.vue'),
        },
        {
          path: 'customers/:id/templates/monthly-report/:templateId',
          name: 'monthly-report-template-editor',
          meta: { title: 'routes.monthlyReportTemplate', section: 'customers', parent: { label: 'routes.customers', to: '/customers' }, kind: 'monthly-report' },
          component: () => import('../views/templates/TemplateEditor.vue'),
        },
        {
          path: 'customers/:id/templates/email/:templateId',
          name: 'email-template-editor',
          meta: { title: 'routes.emailTemplate', section: 'customers', parent: { label: 'routes.customers', to: '/customers' }, kind: 'email' },
          component: () => import('../views/templates/TemplateEditor.vue'),
        },
        {
          path: 'templates',
          name: 'shared-templates',
          meta: { title: 'routes.sharedTemplates', section: 'templates' },
          component: () => import('../views/templates/SharedTemplates.vue'),
        },
        {
          // Templates shared by all customers: the editor without a customer in the URL
          path: 'templates/invoice/:templateId',
          name: 'shared-invoice-template-editor',
          meta: { title: 'routes.sharedInvoiceTemplate', section: 'templates', parent: { label: 'routes.templates', to: '/templates' }, kind: 'invoice' },
          component: () => import('../views/templates/TemplateEditor.vue'),
        },
        {
          path: 'templates/monthly-report/:templateId',
          name: 'shared-monthly-report-template-editor',
          meta: { title: 'routes.sharedMonthlyReportTemplate', section: 'templates', parent: { label: 'routes.templates', to: '/templates' }, kind: 'monthly-report' },
          component: () => import('../views/templates/TemplateEditor.vue'),
        },
        {
          path: 'templates/email/:templateId',
          name: 'shared-email-template-editor',
          meta: { title: 'routes.sharedEmailTemplate', section: 'templates', parent: { label: 'routes.templates', to: '/templates' }, kind: 'email' },
          component: () => import('../views/templates/TemplateEditor.vue'),
        },
        {
          path: 'invoices',
          name: 'invoices',
          meta: { title: 'routes.invoices', section: 'invoices' },
          component: () => import('../views/invoices/InvoicesList.vue'),
        },
        {
          path: 'invoices/new',
          name: 'invoice-generate',
          meta: { title: 'routes.newInvoice', section: 'invoices', parent: { label: 'routes.invoices', to: '/invoices' } },
          component: () => import('../views/invoices/InvoiceGenerate.vue'),
        },
        {
          path: 'invoices/:id',
          name: 'invoice-details',
          meta: { title: 'routes.invoiceDetails', section: 'invoices', parent: { label: 'routes.invoices', to: '/invoices' } },
          component: () => import('../views/invoices/InvoiceDetails.vue'),
        },
        {
          path: 'tax',
          name: 'tax-prediction',
          meta: { title: 'routes.taxPrediction', section: 'tax' },
          component: () => import('../views/tax/TaxPrediction.vue'),
        },
        {
          path: 'expenses',
          name: 'expenses',
          meta: { title: 'routes.expenses', section: 'expenses' },
          component: () => import('../views/expenses/Expenses.vue'),
        },
        {
          path: 'settings',
          meta: { title: 'routes.settings', section: 'settings' },
          component: () => import('../views/settings/Settings.vue'),
          children: [
            { path: '', name: 'settings', redirect: { name: 'settings-general' } },
            {
              path: 'general',
              name: 'settings-general',
              meta: { title: 'routes.settingsGeneral' },
              component: () => import('../views/settings/GeneralSettings.vue'),
            },
            {
              path: 'invoices',
              name: 'settings-invoices',
              meta: { title: 'routes.settingsInvoices' },
              component: () => import('../views/settings/InvoiceSettings.vue'),
            },
            {
              path: 'country',
              name: 'settings-country',
              meta: { title: 'routes.settingsCountry' },
              component: () => import('../views/settings/CountrySettings.vue'),
            },
            {
              path: 'email',
              name: 'settings-email',
              meta: { title: 'routes.settingsEmail' },
              component: () => import('../views/settings/EmailSettings.vue'),
            },
            {
              path: 'data',
              name: 'settings-data',
              meta: { title: 'routes.settingsData' },
              component: () => import('../views/settings/DataSettings.vue'),
            },
          ],
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

  if (to.name === 'access-pending') {
    if (!authStore.isAuthenticated) {
      await authStore.login();
      return;
    }
    return authStore.isApproved ? { path: '/' } : true;
  }

  if (requiresAuth && !authStore.isApproved) {
    return { name: 'access-pending' };
  }
  
  return true;
});

// Browser tab title follows the page title
router.afterEach((to, from) => {
  // Query-only changes (e.g. switching tabs) keep the page's own title
  if (to.path === from.path && from.matched.length > 0) return;
  clearPageTitle();
  const title = to.meta.title as string | undefined;
  document.title = title ? `${t(title)} · nInvoices` : 'nInvoices';
});

export default router;
