<template>
  <div class="d-flex flex-wrap ga-3">
    <v-card v-for="c in cards" :key="c.label" border flat class="px-4 py-2" min-width="170">
      <div class="text-caption text-medium-emphasis">{{ c.label }}</div>
      <div class="text-h6" :class="c.class">{{ c.value }}</div>
    </v-card>
  </div>
</template>

<script setup>
  import { computed } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, kg } from '@/utils/consumables'

  const store = useConsumableStore()

  const cards = computed(() => {
    const t = store.cardTotals
    return [
      { label: `${COLUMN.receiveQty} (filtered)`, value: `${kg(t.receivedKg)} kg` },
      { label: `${COLUMN.take} (filtered)`, value: `${kg(t.issuedKg)} kg` },
      { label: `${COLUMN.balance} (filtered)`, value: `${kg(t.balanceKg)} kg`, class: 'font-weight-bold' },
      { label: 'Low stock consumables', value: t.lowConsumables.size, class: t.lowConsumables.size ? 'text-warning' : '' },
    ]
  })
</script>
