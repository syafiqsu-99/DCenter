<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-3">
      <v-btn-toggle :model-value="dashboardMonths" mandatory divided density="compact" variant="outlined" color="primary"
                    @update:model-value="store.loadDashboard($event)">
        <v-btn v-for="m in [3, 6, 12]" :key="m" :value="m" size="small">{{ m }} months</v-btn>
      </v-btn-toggle>
      <span v-if="dashboard" class="text-caption text-medium-emphasis">
        Only reports marked "Report Required" are counted.
      </span>
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loadingDashboard"
             @click="store.loadDashboard()">
        Refresh
      </v-btn>
    </div>
    <v-alert v-if="dashboardError" type="error" variant="tonal" density="compact" class="mt-3">{{ dashboardError }}</v-alert>
  </div>
</template>

<script setup>
  import { onMounted } from 'vue'
  import { storeToRefs } from 'pinia'
  import { useReportInsightsStore } from '@/store/reportInsightsStore'

  const store = useReportInsightsStore()
  const { dashboard, dashboardMonths, loadingDashboard, dashboardError } = storeToRefs(store)

  onMounted(() => store.loadDashboard())
</script>
