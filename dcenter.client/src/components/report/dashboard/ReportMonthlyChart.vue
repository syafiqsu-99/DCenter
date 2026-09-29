<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1">Monthly output</v-card-title>
    <v-card-subtitle>Reports completed and joints welded per month</v-card-subtitle>
    <v-card-text style="height:300px;">
      <Bar v-if="data" :data="data" :options="options" />
      <v-skeleton-loader v-else type="image" />
    </v-card-text>
  </v-card>
</template>

<script setup>
  import { computed } from 'vue'
  import { Bar } from 'vue-chartjs'
  import { useReportInsightsStore } from '@/store/reportInsightsStore'
  import { JOINTS_COLOR, REPORTS_COLOR } from '@/components/common/chartSetup'

  const store = useReportInsightsStore()

  const data = computed(() => {
    const monthly = store.dashboard?.monthly
    if (!monthly) return null
    return {
      labels: monthly.map((m) => m.month),
      datasets: [
        { label: 'Reports completed', data: monthly.map((m) => m.reports), backgroundColor: REPORTS_COLOR, yAxisID: 'y' },
        { label: 'Joints welded', data: monthly.map((m) => m.joints), backgroundColor: JOINTS_COLOR, yAxisID: 'y1' },
      ],
    }
  })

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom' } },
    scales: {
      y: { beginAtZero: true, position: 'left', ticks: { precision: 0 }, title: { display: true, text: 'Reports' } },
      y1: { beginAtZero: true, position: 'right', ticks: { precision: 0 }, grid: { drawOnChartArea: false }, title: { display: true, text: 'Joints' } },
    },
  }
</script>
