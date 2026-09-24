<template>
  <v-card border flat>
    <v-card-title class="text-subtitle-1 d-flex align-center flex-wrap ga-2">
      <v-icon color="deep-orange" class="me-1">mdi-fire</v-icon>
      Consumption &amp; stock cover
      <span class="text-caption text-medium-emphasis">Used = pickups − returns + finished · cover = on hand ÷ average daily use</span>
      <v-spacer />
      <v-btn-toggle v-model="coverFilter" density="compact" variant="outlined" color="warning" divided>
        <v-btn :value="null" size="small">All</v-btn>
        <v-btn :value="COVER_WARN_DAYS" size="small">Cover &lt; {{ COVER_WARN_DAYS }} days</v-btn>
      </v-btn-toggle>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="itemId" class="consumable-table" density="compact"
                          fixed-header height="360" :loading="!store.dashboard" :sort-by="[{ key: 'averageMonthlyKg', order: 'desc' }]"
                          no-data-text="No consumption recorded in the last 3 months.">
      <template #item.diaSpec="{ item }">
        <strong>{{ item.diaSpec }}</strong>
        <div class="text-caption text-medium-emphasis">{{ item.category }}</div>
      </template>
      <template #item.thisMonthKg="{ item }">{{ kg(item.thisMonthKg) }}</template>
      <template #item.lastMonthKg="{ item }">{{ kg(item.lastMonthKg) }}</template>
      <template #item.averageMonthlyKg="{ item }"><strong>{{ kg(item.averageMonthlyKg) }}</strong></template>
      <template #item.onHandKg="{ item }">
        <span :class="{ 'text-warning font-weight-bold': item.isLow }">{{ kg(item.onHandKg) }}</span>
        <div v-if="item.minStockKg > 0" class="text-caption text-medium-emphasis">min {{ kg(item.minStockKg) }}</div>
      </template>
      <template #item.coverDays="{ item }">
        <v-chip v-if="item.coverDays !== null" size="small" label variant="tonal" :color="coverColor(item.coverDays)">
          {{ item.coverDays }} days
        </v-chip>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { computed, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { kg } from '@/utils/consumables'

  const COVER_WARN_DAYS = 30
  const COVER_CRITICAL_DAYS = 14

  const store = useConsumableStore()
  const coverFilter = ref(null)

  const month = computed(() => store.dashboard?.kpis.monthLabel ?? 'This month')
  const rows = computed(() =>
    (store.dashboard?.itemUsage ?? []).filter((r) => coverFilter.value === null || (r.coverDays !== null && r.coverDays < coverFilter.value)))

  const headers = computed(() => [
    { title: 'Consumable', key: 'diaSpec', width: '28%' },
    { title: `Used ${month.value} (KG)`, key: 'thisMonthKg', align: 'end', width: '13%' },
    { title: 'Used last month (KG)', key: 'lastMonthKg', align: 'end', width: '13%' },
    { title: 'Avg / month, 3 mo (KG)', key: 'averageMonthlyKg', align: 'end', width: '15%' },
    { title: 'On hand (KG)', key: 'onHandKg', align: 'end', width: '15%' },
    { title: 'Cover', key: 'coverDays', align: 'end', width: '16%' },
  ])

  function coverColor(days) {
    if (days < COVER_CRITICAL_DAYS) return 'error'
    if (days < COVER_WARN_DAYS) return 'warning'
    return 'success'
  }
</script>
