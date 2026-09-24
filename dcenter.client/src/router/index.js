import { createRouter, createWebHistory } from 'vue-router';
import { useConsumableStore } from '@/store/consumableStore';

const supervisor = { supervisor: true };

const routes = [
  { path: '/', name: 'report', component: () => import('@/views/report/ReportView.vue') },
  {
    path: '/consumables',
    component: () => import('@/views/consumables/ConsumablesView.vue'),
    redirect: { name: 'consumable-welder' },
    children: [
      { path: 'station', name: 'consumable-welder', component: () => import('@/views/consumables/ConsumableWelderView.vue') },
      { path: 'dashboard', name: 'consumable-dashboard', meta: supervisor, component: () => import('@/views/consumables/ConsumableDashboardView.vue') },
      { path: 'receive', name: 'consumable-receive', meta: supervisor, component: () => import('@/views/consumables/ConsumableReceiveView.vue') },
      { path: 'inventory', name: 'consumable-inventory', meta: supervisor, component: () => import('@/views/consumables/ConsumableInventoryView.vue') },
      { path: 'transfer', name: 'consumable-transfer', meta: supervisor, component: () => import('@/views/consumables/ConsumableTransferView.vue') },
      { path: 'baking', name: 'consumable-baking', meta: supervisor, component: () => import('@/views/consumables/ConsumableBakingView.vue') },
      { path: 'ovens', name: 'consumable-ovens', meta: supervisor, component: () => import('@/views/consumables/ConsumableOvensView.vue') },
      { path: 'counter', redirect: { name: 'consumable-welder' } },
      { path: 'items', name: 'consumable-items', meta: supervisor, component: () => import('@/views/consumables/ConsumableItemsView.vue') },
      { path: 'count', name: 'consumable-count', meta: supervisor, component: () => import('@/views/consumables/ConsumableStockCountView.vue') },
      { path: 'history', name: 'consumable-history', meta: supervisor, component: () => import('@/views/consumables/ConsumableHistoryView.vue') },
      { path: 'issue', redirect: { name: 'consumable-welder' } },
    ],
  },
  {
    path: '/print/consumables/:kind',
    name: 'consumable-print',
    meta: supervisor,
    component: () => import('@/views/consumables/ConsumablePrintView.vue'),
  },
  { path: '/settings', name: 'settings', meta: supervisor, component: () => import('@/views/settings/SettingsView.vue') },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

const needsSupervisor = (route) => route.matched.some((r) => r.meta.supervisor);

router.beforeEach((to, from) => {
  if (!needsSupervisor(to)) return true;
  const store = useConsumableStore();
  if (!store.supervisor) store.init();
  store.tick();
  if (store.isSupervisor) return true;
  const unlock = { unlock: '1', next: to.fullPath };
  if (from.matched.length && !needsSupervisor(from)) return { path: from.path, query: { ...from.query, ...unlock } };
  return { name: 'consumable-welder', query: unlock };
});

export default router;
