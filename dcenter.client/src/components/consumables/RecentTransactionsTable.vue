<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      Recent transactions
      <v-spacer />
      <v-btn size="small" variant="text" :to="{ name: 'consumable-history' }">View all</v-btn>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="id" class="consumable-table" density="compact"
                          fixed-header height="300" :loading="!store.dashboard" no-data-text="No transactions yet.">
      <template #item.txnDate="{ item }">{{ fmtDate(item.txnDate) }}</template>
      <template #item.txnType="{ item }"><TxnTypeChip :type="item.txnType" :voided="item.isVoided" /></template>
      <template #item.diaSpec="{ item }">
        <span :class="{ 'text-disabled': item.isVoided }">{{ item.diaSpec }}</span>
        <div class="text-caption text-medium-emphasis">{{ stageFlow(item) }} · Lot {{ item.lotNumber }}</div>
      </template>
      <template #item.quantityKg="{ item }">{{ kg(item.quantityKg) }}</template>
      <template #item.who="{ item }">{{ item.welderName || item.requestor || '—' }}</template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, fmtDate, kg, stageFlow } from '@/utils/consumables'
  import TxnTypeChip from '@/components/consumables/TxnTypeChip.vue'

  const store = useConsumableStore()
  const rows = computed(() => store.dashboard?.recent ?? [])

  const headers = [
    { title: COLUMN.date, key: 'txnDate', sortable: false, width: '14%' },
    { title: 'Type', key: 'txnType', sortable: false, width: '15%' },
    { title: 'Consumable', key: 'diaSpec', sortable: false, width: '37%' },
    { title: 'KG', key: 'quantityKg', sortable: false, align: 'end', width: '12%' },
    { title: 'Welder / Requestor', key: 'who', sortable: false, width: '22%' },
  ]
</script>
