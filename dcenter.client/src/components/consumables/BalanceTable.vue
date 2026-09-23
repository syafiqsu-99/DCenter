<template>
  <v-card border flat>
    <v-card-title class="text-subtitle-1 d-flex align-center">
      Current inventory balance
      <v-spacer />
      <v-btn size="small" variant="text" :to="{ name: 'consumable-inventory' }">Open inventory</v-btn>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="consumableId" class="consumable-table" density="compact"
                          fixed-header height="360" :loading="!store.dashboard"
                          :sort-by="[{ key: 'balanceKg', order: 'desc' }]" no-data-text="No consumables yet.">
      <template #item.balanceKg="{ item }">
        <span class="font-weight-bold" :class="{ 'text-warning': item.isLow }">{{ kg(item.balanceKg) }}</span>
      </template>
      <template #item.minStockKg="{ item }">{{ item.minStockKg > 0 ? kg(item.minStockKg) : '—' }}</template>
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
  const rows = computed(() => store.dashboard?.balances ?? [])

  const headers = [
    { title: COLUMN.diaSpec, key: 'diaSpec', width: '26%' },
    { title: COLUMN.brand, key: 'brand', width: '20%' },
    { title: COLUMN.type, key: 'consumableType', width: '18%' },
    { title: COLUMN.balance, key: 'balanceKg', align: 'end', width: '12%' },
    { title: 'Min', key: 'minStockKg', align: 'end', width: '10%' },
    { title: 'Last issued', key: 'lastIssuedOn', width: '14%' },
  ]
</script>
