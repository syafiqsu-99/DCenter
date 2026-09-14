import { createRouter, createWebHistory } from 'vue-router';
import Dashboard from '@/views/ReportView.vue'
import Setting from '@/views/SettingsView.vue'

const routes = [
  { path: '/', name: 'report', component: Dashboard },
  { path: '/settings', name: 'settings', component: Setting },
];

export default createRouter({
  history: createWebHistory(),
  routes,
});
