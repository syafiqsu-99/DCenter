<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      <v-icon color="indigo" class="me-2">mdi-view-grid-outline</v-icon>
      Holding oven inventory
      <v-spacer />
      <v-btn size="small" variant="text" :to="{ name: 'consumable-ovens' }">Open ovens</v-btn>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="key" class="consumable-table" density="compact"
                          fixed-header height="300" :loading="store.loadingOvens" no-data-text="The holding ovens are empty.">
      <template #item.code="{ item }">
        <v-chip size="small" label variant="tonal" :color="item.compartmentId ? 'indigo' : 'warning'">{{ item.code }}</v-chip>
      </template>
      <template #item.diaSpec="{ item }">
        {{ item.diaSpec }}
        <div class="text-caption text-medium-emphasis">{{ item.brand }} · Lot {{ item.lotNumber }}</div>
      </template>
      <template #item.kg="{ item }">{{ kg(item.kg) }}</template>
      <template #item.hours="{ item }">{{ item.sinceAt ? elapsed(item.sinceAt) : '—' }}</template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { UNASSIGNED, elapsed, kg } from '@/utils/consumables'

  const store = useConsumableStore()

  const rows = computed(() => {
    const inOvens = store.ovenBoard.ovens.flatMap((o) =>
      o.compartments.flatMap((c) => c.contents.map((x) => ({ ...x, key: `${c.id}:${x.lotId}`, code: c.code }))))
    const unassigned = store.ovenBoard.unassigned.map((x) => ({ ...x, key: `u:${x.lotId}`, code: UNASSIGNED }))
    return [...unassigned, ...inOvens.sort((a, b) => (a.sinceAt ?? '').localeCompare(b.sinceAt ?? ''))]
  })

  const headers = [
    { title: 'Compartment', key: 'code', width: '20%' },
    { title: 'Consumable', key: 'diaSpec', width: '46%' },
    { title: 'KG', key: 'kg', align: 'end', width: '14%' },
    { title: 'Held', key: 'hours', sortable: false, align: 'end', width: '20%' },
  ]
</script>
