<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      <v-icon color="deep-orange" class="me-2">mdi-scale-unbalanced</v-icon>
      Recent finishes &amp; adjustments
      <v-spacer />
      <v-btn size="small" variant="text" @click="openHistory">View all</v-btn>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="id" class="consumable-table" density="compact"
                          fixed-header height="300" :loading="!store.dashboard"
                          no-data-text="No finishes or adjustments recorded.">
      <template #item.txnDate="{ item }">{{ fmtDate(item.txnDate) }}</template>
      <template #item.txnType="{ item }"><TxnTypeChip :type="item.txnType" /></template>
      <template #item.diaSpec="{ item }">
        {{ item.diaSpec }}
        <div class="text-caption text-medium-emphasis">{{ stageFlow(item) }} · Lot {{ item.lotNumber }}</div>
      </template>
      <template #item.quantityKg="{ item }">{{ kg(item.quantityKg) }}</template>
      <template #item.reason="{ item }">
        {{ item.reason || '—' }}
        <div class="text-caption text-medium-emphasis">{{ item.createdBy }}</div>
      </template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed } from 'vue'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, fmtDate, kg, stageFlow } from '@/utils/consumables'
  import TxnTypeChip from '@/components/consumables/TxnTypeChip.vue'

  const store = useConsumableStore()
  const router = useRouter()
  const rows = computed(() => store.dashboard?.variances ?? [])

  const headers = [
    { title: COLUMN.date, key: 'txnDate', sortable: false, width: '14%' },
    { title: 'Type', key: 'txnType', sortable: false, width: '15%' },
    { title: 'Consumable', key: 'diaSpec', sortable: false, width: '36%' },
    { title: 'KG', key: 'quantityKg', sortable: false, align: 'end', width: '12%' },
    { title: 'Reason', key: 'reason', sortable: false, width: '23%' },
  ]

  function openHistory() {
    const k = store.dashboard?.kpis
    store.showHistory({ type: 'Adjust', from: k?.monthStart, to: k?.today, category: store.dashboardCategory })
    router.push({ name: 'consumable-history' })
  }
</script>
