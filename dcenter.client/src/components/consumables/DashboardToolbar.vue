<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-3">
      <v-btn-toggle :model-value="store.dashboardCategory" mandatory divided density="compact" variant="outlined" color="primary"
                    @update:model-value="load">
        <v-btn :value="ALL" size="small">All</v-btn>
        <v-btn v-for="c in store.catalog.categories" :key="c" :value="c" size="small">{{ c }}</v-btn>
      </v-btn-toggle>
      <span v-if="store.dashboard" class="text-caption text-medium-emphasis">
        As of {{ fmtDate(store.dashboard.kpis.today) }}
      </span>
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="store.loadingDashboard" @click="load(store.dashboardCategory)">
        Refresh
      </v-btn>
    </div>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
  </div>
</template>

<script setup>
  import { onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { ALL, errorText, fmtDate } from '@/utils/consumables'

  const store = useConsumableStore()
  const error = ref('')

  async function load(category) {
    error.value = ''
    try {
      await store.loadDashboard(category)
    } catch (e) {
      error.value = errorText(e, 'Could not load the dashboard.')
    }
  }

  onMounted(() => load(store.dashboardCategory))
</script>
