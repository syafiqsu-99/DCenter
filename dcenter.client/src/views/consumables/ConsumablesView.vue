<template>
  <div class="consumables-shell">
    <div class="d-flex flex-wrap align-center ga-3 mb-3 flex-shrink-0">
      <v-tabs v-if="store.isSupervisor" :model-value="route.name" color="primary" density="comfortable" show-arrows class="flex-grow-1">
        <v-tab v-for="t in supervisorTabs" :key="t.name" :value="t.name" :to="{ name: t.name }" :prepend-icon="t.icon">
          {{ t.label }}
        </v-tab>
      </v-tabs>
      <div v-else class="text-h5 font-weight-bold">Welding Consumables</div>
    </div>

    <v-alert v-if="loadError" type="error" variant="tonal" class="mb-3 flex-shrink-0">
      {{ loadError }}
      <template #append>
        <v-btn variant="text" size="small" @click="load">Retry</v-btn>
      </template>
    </v-alert>

    <div class="consumables-content">
      <router-view v-if="store.catalogLoaded" />
      <v-skeleton-loader v-else-if="!loadError" type="card, table" />
    </div>
  </div>
</template>

<script setup>
  import { onMounted, ref } from 'vue'
  import { useRoute } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'

  const route = useRoute()
  const store = useConsumableStore()
  const loadError = ref('')

  const supervisorTabs = [
    { name: 'consumable-welder', label: 'Welder View', icon: 'mdi-account-hard-hat' },
    { name: 'consumable-dashboard', label: 'Dashboard', icon: 'mdi-view-dashboard-outline' },
    { name: 'consumable-receive', label: 'Receiving', icon: 'mdi-tray-arrow-down' },
    { name: 'consumable-stock', label: 'Stock', icon: 'mdi-warehouse' },
    { name: 'consumable-baking', label: 'Baking & Holding', icon: 'mdi-fire' },
    { name: 'consumable-records', label: 'Audit & History', icon: 'mdi-history' },
    { name: 'consumable-items', label: 'Consumables', icon: 'mdi-database-cog-outline' },
  ]

  async function load() {
    loadError.value = ''
    try {
      await store.loadCatalog()
    } catch {
      loadError.value = 'Could not load consumable settings from the server.'
    }
  }

  onMounted(() => {
    if (!store.supervisor) store.init()
    load()
  })
</script>
