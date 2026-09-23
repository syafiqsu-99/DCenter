<template>
  <div class="d-flex flex-wrap align-center ga-3">
    <LocationToggle :model-value="store.dashboardLocation" allow-all @update:model-value="load" />
    <span v-if="store.dashboard" class="text-caption text-medium-emphasis">
      As of {{ fmtDate(store.dashboard.kpis.today) }} · low stock is checked across both locations
    </span>
    <v-spacer />
    <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="store.loadingDashboard" @click="load(store.dashboardLocation)">
      Refresh
    </v-btn>
  </div>
  <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-4">{{ error }}</v-alert>
</template>

<script setup>
  import { onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, fmtDate } from '@/utils/consumables'
  import LocationToggle from '@/components/consumables/LocationToggle.vue'

  const store = useConsumableStore()
  const error = ref('')

  async function load(location) {
    error.value = ''
    try {
      await store.loadDashboard(location)
    } catch (e) {
      error.value = errorText(e, 'Could not load the dashboard.')
    }
  }

  onMounted(() => load(store.dashboardLocation))
</script>
