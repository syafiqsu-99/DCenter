import { createRouter, createWebHistory } from 'vue-router';

const routes = [
  { path: '/', name: 'reports', component: () => import('@/views/ReportView.vue') },
  { path: '/welders', name: 'welders', component: () => import('@/views/WeldersView.vue') },
  { path: '/lookups', name: 'lookups', component: () => import('@/views/LookupsView.vue') },
];

export default createRouter({
  history: createWebHistory(),
  routes,
});
