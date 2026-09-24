<template>
  <v-card border flat>
    <v-card-title class="d-flex align-center flex-wrap ga-2 text-subtitle-1">
      Stock by consumable (Normal + Activated)
      <v-spacer />
      <v-btn-toggle v-model="mode" mandatory divided density="compact" variant="outlined" color="primary">
        <v-btn value="low" size="small">Lowest {{ FIT_COUNT }}</v-btn>
        <v-btn value="high" size="small">Highest {{ FIT_COUNT }}</v-btn>
        <v-btn value="all" size="small">All ({{ rows.length }})</v-btn>
      </v-btn-toggle>
    </v-card-title>
    <v-card-text>
      <v-skeleton-loader v-if="!store.dashboard" type="image" />
      <div v-else-if="!rows.length" class="text-medium-emphasis text-body-2 py-8 text-center">No stock yet.</div>
      <div v-else class="overflow-x-auto">
        <div :style="{ width: chartWidth, height: '320px', position: 'relative' }">
          <Bar :key="`${mode}-${shown.length}`" :data="data" :options="options" />
        </div>
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup>
  import { computed, ref } from 'vue'
  import { Bar } from 'vue-chartjs'
  import { useConsumableStore } from '@/store/consumableStore'
  import { ACTIVATED_COLOR, BAKING_COLOR, NORMAL_COLOR, kgTooltip } from '@/components/consumables/dashboard/chartSetup'

  const FIT_COUNT = 12
  const BAR_SLOT_PX = 64

  const store = useConsumableStore()
  const mode = ref('low')

  const rows = computed(() => (store.dashboard?.balances ?? []).filter((r) => r.isActive))

  const shown = computed(() => {
    const byTotal = [...rows.value].sort((a, b) => a.totalKg - b.totalKg)
    if (mode.value === 'low') return byTotal.slice(0, FIT_COUNT)
    if (mode.value === 'high') return byTotal.reverse().slice(0, FIT_COUNT)
    return byTotal.reverse()
  })

  const chartWidth = computed(() => (shown.value.length > FIT_COUNT ? `${shown.value.length * BAR_SLOT_PX}px` : '100%'))

  const data = computed(() => ({
    labels: shown.value.map((r) => r.diaSpec),
    datasets: [
      { label: 'Normal', data: shown.value.map((r) => r.normalKg), backgroundColor: NORMAL_COLOR, stack: 'stock', maxBarThickness: 44 },
      { label: 'Baking', data: shown.value.map((r) => r.bakingKg), backgroundColor: BAKING_COLOR, stack: 'stock', maxBarThickness: 44 },
      { label: 'Activated', data: shown.value.map((r) => r.activatedKg), backgroundColor: ACTIVATED_COLOR, stack: 'stock', maxBarThickness: 44 },
    ],
  }))

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom' }, tooltip: kgTooltip },
    scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true, title: { display: true, text: 'kg' } } },
  }
</script>
