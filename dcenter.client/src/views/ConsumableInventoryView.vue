<template>
  <div>
    <StickyBar><InventoryFilters /></StickyBar>
    <BalanceSummaryTable v-if="store.inventoryFilters.view === 'items'" />
    <LotStockTable v-else />
  </div>
</template>

<script setup>
  import { useConsumableStore } from '@/store/consumableStore'
  import StickyBar from '@/components/StickyBar.vue'
  import InventoryFilters from '@/components/consumables/InventoryFilters.vue'
  import BalanceSummaryTable from '@/components/consumables/BalanceSummaryTable.vue'
  import LotStockTable from '@/components/consumables/LotStockTable.vue'

  const store = useConsumableStore()
</script>
