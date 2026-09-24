<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      <v-icon color="primary" class="me-2">mdi-warehouse</v-icon>
      Inventory balances
      <v-spacer />
      <v-btn size="small" variant="text" :to="{ name: 'consumable-inventory' }">Open inventory</v-btn>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="itemId" class="consumable-table" density="compact"
                          fixed-header height="300" :loading="!store.dashboard" no-data-text="No stock on hand.">
      <template #item.diaSpec="{ item }">
        <strong>{{ item.diaSpec }}</strong>
        <div class="text-caption text-medium-emphasis">{{ item.category }}</div>
      </template>
      <template #item.normalKg="{ item }">{{ kg(item.normalKg) }}</template>
      <template #item.bakingKg="{ item }">{{ item.bakingKg ? kg(item.bakingKg) : '—' }}</template>
      <template #item.activatedKg="{ item }">{{ kg(item.activatedKg) }}</template>
      <template #item.totalKg="{ item }">
        <strong :class="{ 'text-warning': item.isLow }">{{ kg(item.totalKg) }}</strong>
        <v-icon v-if="item.isLow" color="warning" size="small" class="ms-1">mdi-alert</v-icon>
      </template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { computed } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, kg } from '@/utils/consumables'

  const store = useConsumableStore()
  const rows = computed(() => store.dashboard?.balances ?? [])

  const headers = [
    { title: 'Consumable', key: 'diaSpec', width: '32%' },
    { title: COLUMN.normal, key: 'normalKg', align: 'end', width: '17%' },
    { title: COLUMN.baking, key: 'bakingKg', align: 'end', width: '15%' },
    { title: COLUMN.activated, key: 'activatedKg', align: 'end', width: '18%' },
    { title: COLUMN.total, key: 'totalKg', align: 'end', width: '18%' },
  ]
</script>
