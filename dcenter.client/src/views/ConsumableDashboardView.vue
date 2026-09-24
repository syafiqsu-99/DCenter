<template>
  <div>
    <StickyBar><DashboardToolbar /></StickyBar>
    <KpiCards class="mb-4" />
    <v-row>
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
  import StickyBar from '@/components/StickyBar.vue'
  import DashboardToolbar from '@/components/consumables/DashboardToolbar.vue'
  import KpiCards from '@/components/consumables/KpiCards.vue'
  import ItemUsageTable from '@/components/consumables/ItemUsageTable.vue'
  import LowStockTable from '@/components/consumables/LowStockTable.vue'
  import ActivatedRefillTable from '@/components/consumables/ActivatedRefillTable.vue'
  import MonthlyConsumptionChart from '@/components/consumables/MonthlyConsumptionChart.vue'
  import StockInOutChart from '@/components/consumables/StockInOutChart.vue'
  import InventoryByItemChart from '@/components/consumables/InventoryByItemChart.vue'
  import InventoryBalanceTable from '@/components/consumables/InventoryBalanceTable.vue'
</script>
