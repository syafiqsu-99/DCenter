<template>
  <v-card border flat>
    <v-card-title class="text-subtitle-1 d-flex align-center flex-wrap ga-2">
      Monthly consumption by consumable type (kg)
      <span class="text-caption text-medium-emphasis">Click a month to see each consumable</span>
    </v-card-title>
    <v-card-text style="height:300px;">
      <Bar v-if="data" :data="data" :options="options" />
      <v-skeleton-loader v-else type="image" />
    </v-card-text>

    <v-expand-transition>
      <div v-if="selected">
        <v-divider />
        <div class="d-flex align-center px-4 pt-3">
          <span class="text-subtitle-2">{{ selected.month }} — consumption by consumable</span>
          <v-spacer />
          <v-btn icon="mdi-close" size="small" variant="text" aria-label="Close month details" @click="selected = null" />
        </div>
        <v-alert v-if="detailError" type="error" variant="tonal" density="compact" class="mx-4 mb-3">{{ detailError }}</v-alert>
        <v-data-table :headers="headers" :items="rows" :loading="loadingDetail" item-value="itemId" density="compact"
                      class="consumable-table" :items-per-page="-1" hide-default-footer
                      no-data-text="Nothing was used this month.">
          <template #loading><v-skeleton-loader type="table-row@4" /></template>
          <template #item.diaSpec="{ item }"><strong>{{ item.diaSpec }}</strong></template>
          <template #item.pickedKg="{ item }">{{ kg(item.pickedKg) }}</template>
          <template #item.returnedKg="{ item }">{{ item.returnedKg ? kg(item.returnedKg) : '—' }}</template>
          <template #item.finishedKg="{ item }">{{ item.finishedKg ? kg(item.finishedKg) : '—' }}</template>
          <template #item.netKg="{ item }"><strong>{{ kg(item.netKg) }}</strong></template>
          <template v-if="rows.length" #body.append>
            <tr class="font-weight-bold">
              <td colspan="2">Total</td>
              <td class="text-end">{{ kg(total('pickedKg')) }}</td>
              <td class="text-end">{{ kg(total('returnedKg')) }}</td>
              <td class="text-end">{{ kg(total('finishedKg')) }}</td>
              <td class="text-end">{{ kg(total('netKg')) }}</td>
            </tr>
          </template>
        </v-data-table>
      </div>
    </v-expand-transition>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { computed, ref, watch } from 'vue'
  import { Bar } from 'vue-chartjs'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, errorText, kg } from '@/utils/consumables'
  import { TYPE_COLORS, kgTooltip } from '@/components/consumables/dashboard/chartSetup'

  const store = useConsumableStore()
  const selected = ref(null)
  const rows = ref([])
  const loadingDetail = ref(false)
  const detailError = ref('')
  let requestId = 0

  const headers = [
    { title: 'Consumable', key: 'diaSpec', width: '26%' },
    { title: COLUMN.type, key: 'category', width: '20%' },
    { title: 'Picked (kg)', key: 'pickedKg', align: 'end', width: '13%' },
    { title: 'Returned (kg)', key: 'returnedKg', align: 'end', width: '13%' },
    { title: 'Finished (kg)', key: 'finishedKg', align: 'end', width: '13%' },
    { title: 'Net used (kg)', key: 'netKg', align: 'end', width: '15%' },
  ]

  const total = (key) => rows.value.reduce((s, r) => s + r[key], 0)

  const data = computed(() => {
    const d = store.dashboard
    if (!d) return null
    const faded = (color, i) => (selected.value && d.inOut[i]?.start !== selected.value.start ? `${color}55` : color)
    return {
      labels: d.inOut.map((r) => r.month),
      datasets: d.consumption.map((s, i) => ({
        label: s.category,
        data: s.values,
        baseColor: TYPE_COLORS[i % TYPE_COLORS.length],
        backgroundColor: s.values.map((_, m) => faded(TYPE_COLORS[i % TYPE_COLORS.length], m)),
        stack: 'consumed',
      })),
    }
  })

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    onClick: (_, elements) => {
      const month = store.dashboard?.inOut[elements[0]?.index]
      if (month) select(month)
    },
    onHover: (event, elements) => {
      if (event.native?.target) event.native.target.style.cursor = elements.length ? 'pointer' : 'default'
    },
    plugins: {
      legend: {
        position: 'bottom',
        labels: {
          generateLabels: (chart) => chart.data.datasets.map((ds, i) => ({
            text: ds.label, fillStyle: ds.baseColor, strokeStyle: ds.baseColor, hidden: !chart.isDatasetVisible(i), datasetIndex: i,
          })),
        },
      },
      tooltip: kgTooltip,
    },
    scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true, title: { display: true, text: 'kg' } } },
  }

  async function select(month) {
    if (selected.value?.start === month.start) {
      selected.value = null
      return
    }
    selected.value = month
    await loadDetail()
  }

  async function loadDetail() {
    if (!selected.value) return
    const current = ++requestId
    loadingDetail.value = true
    detailError.value = ''
    try {
      const data = await store.loadMonthConsumption(selected.value.start)
      if (current === requestId) rows.value = data
    } catch (e) {
      if (current === requestId) {
        rows.value = []
        detailError.value = errorText(e, 'Could not load the month details.')
      }
    } finally {
      if (current === requestId) loadingDetail.value = false
    }
  }

  watch(() => store.dashboard, (d) => {
    if (!selected.value || !d) return
    const month = d.inOut.find((m) => m.start === selected.value.start)
    if (!month) selected.value = null
    else loadDetail()
  })
</script>
