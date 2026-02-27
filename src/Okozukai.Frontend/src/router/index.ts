import { createRouter, createWebHistory } from 'vue-router';
import TransactionDashboard from '../components/TransactionDashboard.vue';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'transactions',
      component: TransactionDashboard,
    },
    {
      path: '/dashboard',
      name: 'dashboard',
      component: () => import('../components/dashboard/DashboardPage.vue'),
    },
  ],
});

export default router;
