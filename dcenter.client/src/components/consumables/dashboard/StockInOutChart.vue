<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1">Received vs consumed (kg, last 12 months)</v-card-title>
    <v-card-text style="height:300px;">
      <Bar v-if="data" :data="data" :options="options" />
      <v-skeleton-loader v-else type="image" />
    </v-card-text>
  </v-card>
</template>

<script setup>
  import { computed } from 'vue'
  import { Bar } from 'vue-chartjs'
  import { useConsumableStore } from '@/store/consumableStore'
  import { IN_COLOR, OUT_COLOR, kgTooltip } from '@/components/consumables/dashboard/chartSetup'

  const store = useConsumableStore()

  const data = computed(() => {
    const rows = store.dashboard?.inOut
    if (!rows) return null
    return {
      labels: rows.map((r) => r.month),
      datasets: [
        { label: 'Received', data: rows.map((r) => r.inKg), backgroundColor: IN_COLOR },
        { label: 'Consumed', data: rows.map((r) => r.outKg), backgroundColor: OUT_COLOR },
      ],
    }
  })

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom' }, tooltip: kgTooltip },
    scales: { y: { beginAtZero: true, title: { display: true, text: 'kg' } } },
  }
</script>
