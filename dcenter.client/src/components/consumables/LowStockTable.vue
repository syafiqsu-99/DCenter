<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      <v-icon color="warning" class="me-2">mdi-alert</v-icon>
      Low stock list
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="consumableId" class="consumable-table" density="compact"
                          fixed-header height="320" :loading="!store.dashboard"
                          no-data-text="No consumables are at or below their minimum stock.">
      <template #item.consumable="{ item }">
        {{ item.diaSpec }} · {{ item.brand }}
        <div class="text-caption text-medium-emphasis">{{ item.consumableType }}</div>
      </template>
      <template #item.balanceKg="{ item }">
        <span class="text-warning font-weight-bold">{{ kg(item.balanceKg) }}</span>
      </template>
      <template #item.minStockKg="{ item }">{{ kg(item.minStockKg) }}</template>
      <template #item.shortfallKg="{ item }">{{ kg(item.shortfallKg) }}</template>
      <template #item.lastIssuedOn="{ item }">{{ fmtDate(item.lastIssuedOn) || '—' }}</template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, fmtDate, kg } from '@/utils/consumables'

  const store = useConsumableStore()
  const rows = computed(() => store.dashboard?.lowStock ?? [])

  const headers = [
    { title: 'Consumable', key: 'consumable', sortable: false, width: '40%' },
    { title: COLUMN.balance, key: 'balanceKg', align: 'end', width: '15%' },
    { title: 'Min', key: 'minStockKg', align: 'end', width: '13%' },
    { title: 'Shortfall', key: 'shortfallKg', align: 'end', width: '15%' },
    { title: 'Last issued', key: 'lastIssuedOn', width: '17%' },
  ]
</script>
