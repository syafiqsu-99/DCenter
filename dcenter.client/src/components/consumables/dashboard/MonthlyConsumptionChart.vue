<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1">Monthly consumption by consumable type (kg)</v-card-title>
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
  import { TYPE_COLORS, kgTooltip } from '@/components/consumables/dashboard/chartSetup'

  const store = useConsumableStore()

  const data = computed(() => {
    const d = store.dashboard
    if (!d) return null
    return {
      labels: d.inOut.map((r) => r.month),
      datasets: d.consumption.map((s, i) => ({
        label: s.category,
        data: s.values,
        backgroundColor: TYPE_COLORS[i % TYPE_COLORS.length],
        stack: 'consumed',
      })),
    }
  })

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom' }, tooltip: kgTooltip },
    scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true, title: { display: true, text: 'kg' } } },
  }
</script>
