<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      <v-icon color="warning" class="me-2">mdi-alert</v-icon>
      Low stock
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="itemId" class="consumable-table" density="compact"
                          fixed-header height="300" :loading="!store.dashboard"
                          no-data-text="No consumables are at or below their minimum stock.">
      <template #item.diaSpec="{ item }">
        {{ item.diaSpec }}
        <div class="text-caption text-medium-emphasis">{{ item.category }}</div>
      </template>
      <template #item.totalKg="{ item }"><span class="text-warning font-weight-bold">{{ kg(item.totalKg) }}</span></template>
      <template #item.minStockKg="{ item }">{{ kg(item.minStockKg) }}</template>
      <template #item.shortfall="{ item }">{{ kg(Math.max(item.minStockKg - item.totalKg, 0)) }}</template>
      <template #item.lastIssuedOn="{ item }">{{ fmtDate(item.lastIssuedOn) || '—' }}</template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { fmtDate, kg } from '@/utils/consumables'

  const store = useConsumableStore()
  const rows = computed(() => (store.dashboard?.balances ?? []).filter((r) => r.isLow))

  const headers = [
    { title: 'Consumable', key: 'diaSpec', sortable: false, width: '36%' },
    { title: 'Total', key: 'totalKg', align: 'end', width: '15%' },
    { title: 'Min', key: 'minStockKg', align: 'end', width: '15%' },
    { title: 'Shortfall', key: 'shortfall', align: 'end', sortable: false, width: '15%' },
    { title: 'Last pickup', key: 'lastIssuedOn', width: '19%' },
  ]
</script>
