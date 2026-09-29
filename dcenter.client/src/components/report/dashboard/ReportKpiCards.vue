<template>
  <v-row dense>
    <v-col v-for="c in cards" :key="c.label" cols="12" sm="6" md="4" lg>
      <v-card border flat class="h-100" :color="c.color" :variant="c.color ? 'tonal' : undefined" @click="c.go">
        <v-card-text class="d-flex align-center ga-3">
          <v-icon :icon="c.icon" size="32" :color="c.iconColor" />
          <div>
            <div class="text-caption text-medium-emphasis text-uppercase">{{ c.label }}</div>
            <div class="text-h6 font-weight-bold">
              <v-skeleton-loader v-if="!kpis" type="text" width="80" />
              <template v-else>{{ c.value }}</template>
            </div>
            <div v-if="kpis && c.sub" class="text-caption text-medium-emphasis">{{ c.sub }}</div>
          </div>
        </v-card-text>
      </v-card>
    </v-col>
  </v-row>
</template>

<script setup>
  import { computed } from 'vue'
  import { useRouter } from 'vue-router'
  import { useReportStore } from '@/store/reportStore'
  import { useReportInsightsStore } from '@/store/reportInsightsStore'
  import { REPORT_STATUS } from '@/utils/constants'

  const store = useReportStore()
  const insights = useReportInsightsStore()
  const router = useRouter()
  const kpis = computed(() => insights.dashboard?.kpis ?? null)

  function toList(tab) {
    store.listTab = tab
    router.push({ name: 'report-list' })
  }

  const cards = computed(() => {
    const k = kpis.value
    return [
      {
        label: 'Open drafts', value: k?.openDrafts ?? 0, sub: 'Reports not yet completed',
        icon: 'mdi-file-document-edit-outline', iconColor: 'warning', go: () => toList(REPORT_STATUS.draft),
      },
      {
        label: 'Completed', value: k?.completedThisMonth ?? 0, sub: k?.monthLabel,
        icon: 'mdi-file-check-outline', iconColor: 'success', go: () => toList(REPORT_STATUS.completed),
      },
      {
        label: 'Joints welded', value: k?.jointsThisMonth ?? 0, sub: k ? `${k.monthLabel}, by date welded` : '',
        icon: 'mdi-transit-connection-variant', iconColor: 'primary', go: () => toList('all'),
      },
      {
        label: 'Stale drafts', value: k?.staleDrafts ?? 0, sub: k ? `No update for ${k.staleDays}+ days` : '',
        icon: 'mdi-timer-sand', iconColor: 'deep-orange', color: k?.staleDrafts ? 'deep-orange' : undefined,
        go: () => toList(REPORT_STATUS.draft),
      },
      {
        label: 'Missing date welded', value: k?.missingDateWelded ?? 0, sub: 'Drafts that cannot be completed yet',
        icon: 'mdi-calendar-alert', iconColor: 'error', color: k?.missingDateWelded ? 'error' : undefined,
        go: () => toList(REPORT_STATUS.draft),
      },
    ]
  })
</script>
