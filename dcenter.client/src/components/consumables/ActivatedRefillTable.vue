<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      <v-icon color="info" class="me-2">mdi-swap-horizontal</v-icon>
      Activated storage below minimum
      <v-spacer />
      <v-btn size="small" variant="text" :to="{ name: 'consumable-transfer' }">Transfer</v-btn>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="itemId" class="consumable-table" density="compact"
                          fixed-header height="300" :loading="!store.dashboard"
                          no-data-text="Every rack is at or above its activated minimum.">
      <template #item.diaSpec="{ item }">
        {{ item.diaSpec }}
        <div class="text-caption text-medium-emphasis">{{ item.category }}</div>
      </template>
      <template #item.activatedKg="{ item }"><span class="text-warning font-weight-bold">{{ kg(item.activatedKg) }}</span></template>
      <template #item.activatedMinKg="{ item }">{{ kg(item.activatedMinKg) }}</template>
      <template #item.normalKg="{ item }">{{ kg(item.normalKg) }}</template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { kg } from '@/utils/consumables'

  const store = useConsumableStore()
  const rows = computed(() => (store.dashboard?.balances ?? []).filter((r) => r.needsRefill))

  const headers = [
    { title: 'Consumable', key: 'diaSpec', sortable: false, width: '40%' },
    { title: 'Activated', key: 'activatedKg', align: 'end', width: '20%' },
    { title: 'Activated Min', key: 'activatedMinKg', align: 'end', width: '20%' },
    { title: 'In Normal', key: 'normalKg', align: 'end', width: '20%' },
  ]
</script>
