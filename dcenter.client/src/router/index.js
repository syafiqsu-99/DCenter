import { createRouter, createWebHistory } from 'vue-router';

const routes = [
  { path: '/', name: 'report', component: () => import('@/views/ReportView.vue') },
  {
    path: '/consumables',
    component: () => import('@/views/ConsumablesView.vue'),
    redirect: { name: 'consumable-dashboard' },
    children: [
      { path: 'dashboard', name: 'consumable-dashboard', component: () => import('@/views/ConsumableDashboardView.vue') },
      { path: 'receive', name: 'consumable-receive', component: () => import('@/views/ConsumableReceiveView.vue') },
      { path: 'issue', name: 'consumable-issue', component: () => import('@/views/ConsumableIssueView.vue') },
      { path: 'inventory', name: 'consumable-inventory', component: () => import('@/views/ConsumableInventoryView.vue') },
      { path: 'history', name: 'consumable-history', component: () => import('@/views/ConsumableHistoryView.vue') },
    ],
  },
  { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
];

export default createRouter({
  history: createWebHistory(),
  routes,
});
