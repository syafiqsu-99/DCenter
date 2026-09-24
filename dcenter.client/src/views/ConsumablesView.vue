<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-3 mb-4">
      <v-tabs v-if="store.isSupervisor" :model-value="route.name" color="primary" density="comfortable" show-arrows class="flex-grow-1">
        <v-tab v-for="t in supervisorTabs" :key="t.name" :value="t.name" :to="{ name: t.name }" :prepend-icon="t.icon">
          {{ t.label }}
        </v-tab>
      </v-tabs>
      <div v-else class="text-h5 font-weight-bold">Welding Consumables</div>
    </div>

    <v-alert v-if="loadError" type="error" variant="tonal" class="mb-4">
      {{ loadError }}
      <template #append>
        <v-btn variant="text" size="small" @click="load">Retry</v-btn>
      </template>
    </v-alert>

    <router-view v-if="store.catalogLoaded" />
    <v-skeleton-loader v-else-if="!loadError" type="card, table" />
  </div>
</template>

<script setup>
  import { onMounted, ref, watch } from 'vue'
  import { useRoute, useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'

  const route = useRoute()
  const router = useRouter()
  const store = useConsumableStore()
  const loadError = ref('')

  const supervisorTabs = [
    { name: 'consumable-welder', label: 'Welder View', icon: 'mdi-account-hard-hat' },
    { name: 'consumable-dashboard', label: 'Dashboard', icon: 'mdi-view-dashboard-outline' },
    { name: 'consumable-receive', label: 'Receiving', icon: 'mdi-tray-arrow-down' },
    { name: 'consumable-inventory', label: 'Inventory', icon: 'mdi-warehouse' },
    { name: 'consumable-transfer', label: 'Activated & Transfers', icon: 'mdi-swap-horizontal' },
    { name: 'consumable-baking', label: 'Baking Records', icon: 'mdi-fire' },
    { name: 'consumable-ovens', label: 'Holding', icon: 'mdi-view-grid-outline' },
    { name: 'consumable-items', label: 'Consumables', icon: 'mdi-database-cog-outline' },
    { name: 'consumable-count', label: 'Stock Audit', icon: 'mdi-clipboard-check-outline' },
    { name: 'consumable-history', label: 'History', icon: 'mdi-history' },
  ]

  async function load() {
    loadError.value = ''
    try {
      await store.loadCatalog()
    } catch {
      loadError.value = 'Could not load consumable settings from the server.'
    }
  }

  watch(() => store.isSupervisor, (supervisor) => {
    if (!supervisor && route.matched.some((r) => r.meta.supervisor)) router.push({ name: 'consumable-welder' })
  })

  onMounted(() => {
    if (!store.supervisor) store.init()
    load()
  })
</script>
