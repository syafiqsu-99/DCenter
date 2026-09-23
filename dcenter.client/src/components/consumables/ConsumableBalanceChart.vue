<template>
  <v-card border flat class="h-100">
    <v-card-title class="d-flex align-center flex-wrap ga-2 text-subtitle-1">
      {{ type }}
      <v-spacer />
      <v-btn-toggle v-model="mode" mandatory divided density="compact" variant="outlined" color="primary">
        <v-btn value="low" size="small">Lowest 10</v-btn>
        <v-btn value="high" size="small">Highest 10</v-btn>
        <v-btn value="all" size="small">All ({{ rows.length }})</v-btn>
      </v-btn-toggle>
    </v-card-title>
    <v-card-text>
      <v-skeleton-loader v-if="!store.dashboard" type="image" />
      <div v-else-if="!rows.length" class="text-medium-emphasis text-body-2 py-8 text-center">
        No {{ type }} consumables yet.
      </div>
      <template v-else>
        <div class="chart-scroll">
          <div :style="{ width: chartWidth, height: '320px', position: 'relative' }">
            <Bar :key="`${mode}-${shown.length}`" :data="data" :options="options" />
          </div>
        </div>
        <div class="text-caption text-medium-emphasis mt-2">
          <v-icon :color="LOW_COLOR" size="x-small">mdi-square</v-icon> at or below minimum stock
          <span v-if="mode === 'all' && shown.length > FIT_COUNT"> · scroll sideways to see all items</span>
        </div>
      </template>
    </v-card-text>
  </v-card>
</template>

<script setup>
  import { computed, ref } from 'vue'
  import { Bar } from 'vue-chartjs'
  import { useConsumableStore } from '@/store/consumableStore'
  import { BALANCE_COLOR, LOW_COLOR } from '@/components/consumables/chartSetup'

  const props = defineProps({ type: { type: String, required: true } })

  const FIT_COUNT = 10
  const BAR_SLOT_PX = 70

  const store = useConsumableStore()
  const mode = ref('low')

  const rows = computed(() => (store.dashboard?.balances ?? []).filter((r) => r.consumableType === props.type))

  const shown = computed(() => {
    const byBalance = [...rows.value].sort((a, b) => a.balanceKg - b.balanceKg)
    if (mode.value === 'low') return byBalance.slice(0, FIT_COUNT)
    if (mode.value === 'high') return byBalance.reverse().slice(0, FIT_COUNT)
    return byBalance.reverse()
  })

  const chartWidth = computed(() =>
    shown.value.length > FIT_COUNT ? `${shown.value.length * BAR_SLOT_PX}px` : '100%')

  const data = computed(() => ({
    labels: shown.value.map((r) => [r.diaSpec, r.brand]),
    datasets: [{
      label: 'Balance',
      data: shown.value.map((r) => r.balanceKg),
      backgroundColor: shown.value.map((r) => (r.isLow ? LOW_COLOR : BALANCE_COLOR)),
      maxBarThickness: 48,
    }],
  }))

  const options = computed(() => ({
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        callbacks: {
          title: (ctx) => ctx[0]?.label?.split(',').join(' · ') ?? '',
          label: (ctx) => {
            const r = shown.value[ctx.dataIndex]
            const min = r.minStockKg > 0 ? ` (min ${r.minStockKg.toFixed(2)} kg)` : ''
            return `Balance: ${Number(ctx.parsed.y).toFixed(2)} kg${min}`
          },
        },
      },
    },
    scales: {
      x: { ticks: { autoSkip: false, maxRotation: 0, font: { size: 11 } } },
      y: { beginAtZero: true, title: { display: true, text: 'kg' } },
    },
  }))
</script>

<style scoped>
  .chart-scroll {
    overflow-x: auto;
    overflow-y: hidden;
  }
</style>
