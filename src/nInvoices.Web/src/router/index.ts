import { createRouter, createWebHistory } from 'vue-router';

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      component: () => import('../layouts/MainLayout.vue'),
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

export default router;
