<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1">{{ title }}</v-card-title>
    <v-card-subtitle>Joints in the selected period, top 10</v-card-subtitle>
    <v-card-text>
      <v-skeleton-loader v-if="!items" type="list-item@5" />
      <div v-else-if="!items.length" class="text-medium-emphasis text-body-2">No joints recorded in this period.</div>
      <v-list v-else density="compact" class="py-0">
        <v-list-item v-for="row in items" :key="row.name + (row.detail ?? '')" class="px-0"
                     :title="row.name" :subtitle="row.detail" @click="trace(row)">
          <template #append>
            <div class="d-flex align-center ga-2" style="min-width:140px;">
              <v-progress-linear :model-value="(row.count / max) * 100" color="primary" height="6" rounded />
              <strong class="text-body-2" style="min-width:32px;text-align:end;">{{ row.count }}</strong>
            </div>
          </template>
        </v-list-item>
      </v-list>
    </v-card-text>
  </v-card>
</template>

<script setup>
  import { computed } from 'vue'
  import { useRouter } from 'vue-router'
  import { useReportInsightsStore } from '@/store/reportInsightsStore'

  const props = defineProps({
    title: { type: String, required: true },
    items: { type: Array, default: null },
    traceField: { type: String, required: true },
  })

  const store = useReportInsightsStore()
  const router = useRouter()
  const max = computed(() => Math.max(1, ...(props.items ?? []).map((r) => r.count)))

  function trace(row) {
    store.traceField = props.traceField
    store.traceQuery = row.detail || row.name
    store.searchTrace()
    router.push({ name: 'report-trace' })
  }
</script>
