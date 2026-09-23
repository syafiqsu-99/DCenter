<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      Recent transactions
      <v-spacer />
      <v-btn size="small" variant="text" :to="{ name: 'consumable-history' }">View all</v-btn>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="id" class="consumable-table" density="compact"
                          fixed-header height="320" :loading="!store.dashboard" no-data-text="No transactions yet.">
      <template #item.txnDate="{ item }">{{ fmtDate(item.txnDate) }}</template>
      <template #item.txnType="{ item }"><TxnTypeChip :type="item.txnType" :voided="item.isVoided" /></template>
      <template #item.diaSpec="{ item }">
        <span :class="{ 'text-disabled': item.isVoided }">{{ item.diaSpec }}</span>
        <div class="text-caption text-medium-emphasis">Lot {{ item.lotNumber }}</div>
      </template>
      <template #item.quantityKg="{ item }">{{ kg(Math.abs(item.quantityKg)) }}</template>
      <template #item.requestor="{ item }">{{ item.requestor || '—' }}</template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, fmtDate, kg } from '@/utils/consumables'
  import TxnTypeChip from '@/components/consumables/TxnTypeChip.vue'

  const store = useConsumableStore()
  const rows = computed(() => store.dashboard?.recent ?? [])

  const headers = [
    { title: COLUMN.date, key: 'txnDate', sortable: false, width: '14%' },
    { title: 'Type', key: 'txnType', sortable: false, width: '14%' },
    { title: 'Consumable', key: 'diaSpec', sortable: false, width: '30%' },
    { title: 'KG', key: 'quantityKg', sortable: false, align: 'end', width: '10%' },
    { title: COLUMN.source, key: 'location', sortable: false, width: '14%' },
    { title: COLUMN.requestor, key: 'requestor', sortable: false, width: '18%' },
  ]
</script>
