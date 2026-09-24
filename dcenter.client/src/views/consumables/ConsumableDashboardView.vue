<template>
  <div class="consumable-page consumable-page--scroll">
    <StickyBar><DashboardToolbar /></StickyBar>
    <KpiCards />
    <v-row dense>
      <v-col cols="12"><ItemUsageTable /></v-col>
      <v-col cols="12" lg="6"><LowStockTable /></v-col>
      <v-col cols="12" lg="6"><ActivatedRefillTable /></v-col>
      <v-col cols="12" lg="6"><MonthlyConsumptionChart /></v-col>
      <v-col cols="12" lg="6"><StockInOutChart /></v-col>
      <v-col cols="12" lg="6"><InventoryByItemChart /></v-col>
      <v-col cols="12" lg="6"><InventoryBalanceTable /></v-col>
    </v-row>
  </div>
</template>

<script setup>
  import StickyBar from '@/components/common/StickyBar.vue'
  import DashboardToolbar from '@/components/consumables/dashboard/DashboardToolbar.vue'
  import KpiCards from '@/components/consumables/dashboard/KpiCards.vue'
  import ItemUsageTable from '@/components/consumables/dashboard/ItemUsageTable.vue'
  import LowStockTable from '@/components/consumables/dashboard/LowStockTable.vue'
  import ActivatedRefillTable from '@/components/consumables/dashboard/ActivatedRefillTable.vue'
  import MonthlyConsumptionChart from '@/components/consumables/dashboard/MonthlyConsumptionChart.vue'
  import StockInOutChart from '@/components/consumables/dashboard/StockInOutChart.vue'
  import InventoryByItemChart from '@/components/consumables/dashboard/InventoryByItemChart.vue'
  import InventoryBalanceTable from '@/components/consumables/dashboard/InventoryBalanceTable.vue'
</script>
