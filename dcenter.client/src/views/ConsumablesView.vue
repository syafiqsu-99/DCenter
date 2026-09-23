<template>
  <div>
    <v-tabs :model-value="route.name" color="primary" density="comfortable" show-arrows class="mb-4">
      <v-tab v-for="t in tabs" :key="t.name" :value="t.name" :to="{ name: t.name }" :prepend-icon="t.icon">
        {{ t.label }}
      </v-tab>
    </v-tabs>

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
  import { onMounted, ref } from 'vue'
  import { useRoute } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'

  const route = useRoute()
  const store = useConsumableStore()
  const loadError = ref('')

  const tabs = [
    { name: 'consumable-dashboard', label: 'Dashboard', icon: 'mdi-view-dashboard-outline' },
    { name: 'consumable-receive', label: 'Stock In', icon: 'mdi-tray-arrow-down' },
    { name: 'consumable-issue', label: 'Stock Out', icon: 'mdi-tray-arrow-up' },
    { name: 'consumable-inventory', label: 'Inventory', icon: 'mdi-warehouse' },
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

  onMounted(load)
</script>
