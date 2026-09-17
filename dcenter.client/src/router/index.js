import { createRouter, createWebHistory } from 'vue-router';

const routes = [
  { path: '/', name: 'report', component: () => import('@/views/ReportView.vue') },
  { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
];

export default createRouter({
  history: createWebHistory(),
  routes,
});
