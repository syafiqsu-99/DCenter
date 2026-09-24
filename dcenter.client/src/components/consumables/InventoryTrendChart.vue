<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1">Inventory trend (month-end kg, last 12 months)</v-card-title>
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
  import { ACTIVATED_COLOR, BAKING_COLOR, NORMAL_COLOR, kgTooltip } from '@/components/consumables/chartSetup'

  const store = useConsumableStore()

  const data = computed(() => {
    const rows = store.dashboard?.inventoryTrend
    if (!rows) return null
    return {
      labels: rows.map((r) => r.month),
      datasets: [
        { label: 'Normal', data: rows.map((r) => r.normalKg), backgroundColor: NORMAL_COLOR, stack: 'stock' },
        { label: 'Baking', data: rows.map((r) => r.bakingKg), backgroundColor: BAKING_COLOR, stack: 'stock' },
        { label: 'Activated', data: rows.map((r) => r.activatedKg), backgroundColor: ACTIVATED_COLOR, stack: 'stock' },
      ],
    }
  })

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom' },
      tooltip: {
        ...kgTooltip,
        callbacks: {
          ...kgTooltip.callbacks,
          footer: (items) => `Total: ${items.reduce((s, i) => s + Number(i.parsed.y), 0).toFixed(2)} kg`,
        },
      },
    },
    scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true, title: { display: true, text: 'kg' } } },
  }
</script>
