<template>
  <v-card border flat>
    <v-data-table-virtual :headers="headers" :items="store.summaryRows" :loading="store.loadingStockCard"
                          item-value="key" class="consumable-table" density="comfortable" hover fixed-header
                          height="calc(100vh - 400px)" style="min-height:360px;"
                          no-data-text="No stock matches these filters.">
      <template #item.key="{ item }">
        {{ item.key }}
        <v-tooltip v-if="item.isLow" location="top" text="Contains a consumable at or below its minimum stock">
          <template #activator="{ props: p }">
            <v-icon v-bind="p" color="warning" size="small" class="ms-1">mdi-alert</v-icon>
          </template>
        </v-tooltip>
      </template>
      <template #item.receivedKg="{ item }">{{ kg(item.receivedKg) }}</template>
      <template #item.issuedKg="{ item }">{{ kg(item.issuedKg) }}</template>
      <template #item.balanceKg="{ item }">
        <strong :class="{ 'text-warning': item.isLow }">{{ kg(item.balanceKg) }}</strong>
      </template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, kg } from '@/utils/consumables'

  const store = useConsumableStore()

  const groupTitles = {
    consumable: 'Consumable',
    brand: COLUMN.brand,
    spec: COLUMN.spec,
    diameter: COLUMN.diameter,
    lot: COLUMN.lot,
  }

  const headers = computed(() => [
    { title: groupTitles[store.filters.view] ?? 'Group', key: 'key', width: '50%' },
    { title: 'Lots', key: 'lotCount', align: 'end', width: '10%' },
    { title: COLUMN.receiveQty, key: 'receivedKg', align: 'end', width: '14%' },
    { title: COLUMN.take, key: 'issuedKg', align: 'end', width: '13%' },
    { title: COLUMN.balance, key: 'balanceKg', align: 'end', width: '13%' },
  ])
</script>
