<template>
  <v-card border flat>
    <v-card-title class="d-flex align-center flex-wrap ga-2 text-subtitle-1">
      Stock by consumable
      <span class="text-caption text-medium-emphasis">Bars show kg on hand; the red mark is the minimum stock.</span>
      <v-spacer />
      <v-btn-toggle v-model="mode" mandatory divided density="compact" variant="outlined" color="primary">
        <v-btn value="low" size="small" :color="lowRows.length ? 'error' : undefined">Low stock ({{ lowRows.length }})</v-btn>
        <v-btn value="all" size="small">All ({{ rows.length }})</v-btn>
        <v-btn value="high" size="small">Highest {{ FIT_COUNT }}</v-btn>
      </v-btn-toggle>
    </v-card-title>
    <v-card-text>
      <v-skeleton-loader v-if="!store.dashboard" type="image" />
      <div v-else-if="!rows.length" class="text-medium-emphasis text-body-2 py-8 text-center">No stock yet.</div>
      <div v-else-if="!shown.length" class="d-flex flex-column align-center justify-center ga-2 text-body-1" :style="{ height: CHART_HEIGHT }">
        <v-icon color="success" size="40">mdi-check-circle-outline</v-icon>
        No consumables are below their minimum stock.
        <v-btn variant="text" color="primary" @click="mode = 'all'">Show all consumables</v-btn>
      </div>
      <div v-else class="overflow-x-auto">
        <div :style="{ width: chartWidth, height: CHART_HEIGHT, position: 'relative' }">
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
  import { ACTIVATED_COLOR, BAKING_COLOR, MIN_STOCK_COLOR, NORMAL_COLOR } from '@/components/consumables/dashboard/chartSetup'

  const FIT_COUNT = 16
  const BAR_SLOT_PX = 64
  const CHART_HEIGHT = '420px'

  const store = useConsumableStore()
  const mode = ref('low')

  const coverage = (r) => (r.minStockKg > 0 ? r.totalKg / r.minStockKg : Number.POSITIVE_INFINITY)
  const byCoverage = (a, b) => coverage(a) - coverage(b) || a.totalKg - b.totalKg

  const rows = computed(() => (store.dashboard?.balances ?? []).filter((r) => r.isActive))
  const lowRows = computed(() => rows.value.filter((r) => r.isLow))

  const shown = computed(() => {
    if (mode.value === 'low') return [...lowRows.value].sort(byCoverage)
    if (mode.value === 'high') return [...rows.value].sort((a, b) => b.totalKg - a.totalKg).slice(0, FIT_COUNT)
    return [...rows.value].sort(byCoverage)
  })

  const chartWidth = computed(() => (shown.value.length > FIT_COUNT ? `${shown.value.length * BAR_SLOT_PX}px` : '100%'))

  const data = computed(() => ({
    labels: shown.value.map((r) => r.diaSpec),
    datasets: [
      {
        type: 'line', label: 'Min stock', data: shown.value.map((r) => (r.minStockKg > 0 ? r.minStockKg : null)),
        showLine: false, pointStyle: 'line', pointRadius: 16, pointHoverRadius: 18, borderWidth: 3,
        borderColor: MIN_STOCK_COLOR, backgroundColor: MIN_STOCK_COLOR, stack: 'min', order: 0,
      },
      { label: 'Normal', data: shown.value.map((r) => r.normalKg), backgroundColor: NORMAL_COLOR, stack: 'stock', maxBarThickness: 44, order: 1 },
      { label: 'Baking', data: shown.value.map((r) => r.bakingKg), backgroundColor: BAKING_COLOR, stack: 'stock', maxBarThickness: 44, order: 1 },
      { label: 'Activated', data: shown.value.map((r) => r.activatedKg), backgroundColor: ACTIVATED_COLOR, stack: 'stock', maxBarThickness: 44, order: 1 },
    ],
  }))

  const options = computed(() => ({
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: { position: 'bottom' },
      tooltip: {
        callbacks: {
          label: (ctx) => (ctx.parsed.y === null ? null : `${ctx.dataset.label}: ${Number(ctx.parsed.y).toFixed(2)} kg`),
          footer: (items) => {
            const r = shown.value[items[0]?.dataIndex]
            return r ? `Total ${r.totalKg.toFixed(2)} kg${r.isLow ? ' · LOW STOCK' : ''}` : ''
          },
        },
      },
    },
    scales: {
      x: {
        stacked: true,
        ticks: {
          color: (ctx) => (shown.value[ctx.index]?.isLow ? MIN_STOCK_COLOR : undefined),
          font: (ctx) => (shown.value[ctx.index]?.isLow ? { weight: 'bold' } : undefined),
        },
      },
      y: { stacked: true, beginAtZero: true, title: { display: true, text: 'kg' } },
    },
  }))
</script>
