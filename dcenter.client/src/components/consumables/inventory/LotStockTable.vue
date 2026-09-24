<template>
  <v-card border flat class="fill-card">
    <div ref="tableArea" class="fill">
      <v-data-table-virtual :headers="headers" :items="store.filteredLotStock" :loading="store.loadingLotStock" item-value="lotId"
                            class="consumable-table stock-card" density="compact" fixed-header :height="tableHeight"
                            no-data-text="No lots match the filters.">
        <template #loading><v-skeleton-loader type="table-row@10" /></template>
        <template #item.receivedBy="{ item }">{{ item.receivedBy || '—' }}</template>
        <template #item.date="{ item }">{{ fmtDate(item.date) }}</template>
        <template #item.receiveQtyKg="{ item }">{{ kg(item.receiveQtyKg) }}</template>
        <template #item.takeKg="{ item }">{{ kg(item.takeKg) }}</template>
        <template #item.normalKg="{ item }">{{ kg(item.normalKg) }}</template>
        <template #item.bakingKg="{ item }">{{ item.bakingKg ? kg(item.bakingKg) : '—' }}</template>
        <template #item.activatedKg="{ item }">{{ kg(item.activatedKg) }}</template>
        <template #item.balanceKg="{ item }">
          <span :class="item.isLow ? 'text-warning font-weight-bold' : 'font-weight-bold'">{{ kg(item.balanceKg) }}</span>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-history" variant="text" size="small" :aria-label="`History of lot ${item.lotNumber}`"
                 @click="openHistory(item)" />
        </template>
      </v-data-table-virtual>
    </div>
  </v-card>

  <LotHistoryDialog v-model="historyOpen" :lot="selected" />
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { ref } from 'vue'
  import { useFillHeight } from '@/composables/useFillHeight'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, fmtDate, kg } from '@/utils/consumables'
  import LotHistoryDialog from '@/components/consumables/shared/LotHistoryDialog.vue'

  const tableArea = ref(null)
  const tableHeight = useFillHeight(tableArea)
  const store = useConsumableStore()
  const selected = ref(null)
  const historyOpen = ref(false)

  const headers = [
    { title: COLUMN.receivedBy, key: 'receivedBy', width: '8%' },
    { title: COLUMN.date, key: 'date', width: '7%' },
    { title: COLUMN.source, key: 'source', width: '7%' },
    { title: COLUMN.brand, key: 'brand', width: '9%' },
    { title: COLUMN.diameter, key: 'diameter', width: '7%' },
    { title: COLUMN.spec, key: 'specification', width: '10%' },
    { title: COLUMN.lot, key: 'lotNumber', width: '8%' },
    { title: COLUMN.diaSpec, key: 'diaSpec', width: '10%' },
    { title: COLUMN.receiveQty, key: 'receiveQtyKg', align: 'end', width: '7%' },
    { title: COLUMN.take, key: 'takeKg', align: 'end', width: '6%' },
    { title: COLUMN.normal, key: 'normalKg', align: 'end', width: '6%' },
    { title: COLUMN.baking, key: 'bakingKg', align: 'end', width: '6%' },
    { title: COLUMN.activated, key: 'activatedKg', align: 'end', width: '6%' },
    { title: COLUMN.balance, key: 'balanceKg', align: 'end', width: '6%' },
    { title: '', key: 'actions', sortable: false, align: 'end', width: '3%' },
  ]

  function openHistory(row) {
    selected.value = row
    historyOpen.value = true
  }
</script>

<style scoped>
  .stock-card :deep(th) {
    font-weight: 700 !important;
    white-space: normal;
    line-height: 1.2;
    vertical-align: bottom;
  }
</style>
