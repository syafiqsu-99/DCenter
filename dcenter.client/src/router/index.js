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
      { path: 'stock', name: 'consumable-stock', meta: supervisor, component: () => import('@/views/consumables/ConsumableStockView.vue') },
      { path: 'baking', name: 'consumable-baking', meta: supervisor, component: () => import('@/views/consumables/ConsumableBakingView.vue') },
      { path: 'records', name: 'consumable-records', meta: supervisor, component: () => import('@/views/consumables/ConsumableRecordsView.vue') },
      { path: 'items', name: 'consumable-items', meta: supervisor, component: () => import('@/views/consumables/ConsumableItemsView.vue') },
      { path: 'inventory', redirect: { name: 'consumable-stock' } },
      { path: 'transfer', redirect: { name: 'consumable-stock' } },
      { path: 'ovens', redirect: { name: 'consumable-baking', query: { view: 'ovens' } } },
      { path: 'count', redirect: { name: 'consumable-records', query: { view: 'count' } } },
      { path: 'history', redirect: { name: 'consumable-records' } },
      { path: 'counter', redirect: { name: 'consumable-welder' } },
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
