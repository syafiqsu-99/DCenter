<template>
  <div>
    <InventoryFilters class="mb-3" />
    <InventorySummaryStrip class="mb-3" />
    <StockCardTable v-if="store.filters.view === 'card'" />
    <InventorySummaryTable v-else />
  </div>
</template>

<script setup>
  import { useConsumableStore } from '@/store/consumableStore'
  import InventoryFilters from '@/components/consumables/InventoryFilters.vue'
  import InventorySummaryStrip from '@/components/consumables/InventorySummaryStrip.vue'
  import StockCardTable from '@/components/consumables/StockCardTable.vue'
  import InventorySummaryTable from '@/components/consumables/InventorySummaryTable.vue'

  const store = useConsumableStore()
</script>
