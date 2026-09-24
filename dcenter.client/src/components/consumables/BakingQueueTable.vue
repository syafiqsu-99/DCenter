<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      <v-icon color="purple" class="me-2">mdi-fire</v-icon>
      Baking queue
      <v-spacer />
      <v-btn size="small" variant="text" :to="{ name: 'consumable-baking' }">Open board</v-btn>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="id" class="consumable-table" density="compact"
                          fixed-header height="300" :loading="store.loadingBaking" no-data-text="Nothing queued, baking or awaiting placement.">
      <template #item.diaSpec="{ item }">
        {{ item.diaSpec }}
        <div class="text-caption text-medium-emphasis">{{ item.bakingNo }} · Lot {{ item.lotNumber }}</div>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="BAKING_COLORS[item.status]" variant="tonal" label>{{ BAKING_LABELS[item.status] }}</v-chip>
      </template>
      <template #item.balanceKg="{ item }">{{ kg(item.balanceKg) }}</template>
      <template #item.since="{ item }">{{ age(item) }}</template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { BAKING_COLORS, BAKING_LABELS, elapsed, kg } from '@/utils/consumables'

  const ORDER = ['Baked', 'Rebaked', 'Baking', 'Rebaking', 'Queued', 'RebakeQueued']

  const store = useConsumableStore()
  const rows = computed(() =>
    [...store.bakingBoard].sort((a, b) => ORDER.indexOf(a.status) - ORDER.indexOf(b.status) || a.id - b.id))

  const headers = [
    { title: 'Consumable', key: 'diaSpec', sortable: false, width: '44%' },
    { title: 'Status', key: 'status', sortable: false, width: '22%' },
    { title: 'KG', key: 'balanceKg', sortable: false, align: 'end', width: '14%' },
    { title: 'For', key: 'since', sortable: false, align: 'end', width: '20%' },
  ]

  function age(r) {
    const from = r.rebakeStop ?? r.rebakeStart ?? r.bakeStop ?? r.bakeStart ?? r.createdAt
    return elapsed(from)
  }
</script>
