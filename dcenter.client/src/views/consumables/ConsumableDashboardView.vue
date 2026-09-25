<template>
  <div class="consumable-page consumable-page--scroll">
    <StickyBar><DashboardToolbar /></StickyBar>
    <KpiCards />
    <NeedsActionTable />
    <InventoryByItemChart />
    <v-row dense>
      <v-col cols="12"><ItemUsageTable /></v-col>
      <v-col cols="12"><MonthlyConsumptionChart /></v-col>
      <v-col cols="12"><StockInOutChart /></v-col>
    </v-row>
  </div>
</template>

<script setup>
  import StickyBar from '@/components/common/StickyBar.vue'
  import DashboardToolbar from '@/components/consumables/dashboard/DashboardToolbar.vue'
  import KpiCards from '@/components/consumables/dashboard/KpiCards.vue'
  import NeedsActionTable from '@/components/consumables/dashboard/NeedsActionTable.vue'
  import ItemUsageTable from '@/components/consumables/dashboard/ItemUsageTable.vue'
  import MonthlyConsumptionChart from '@/components/consumables/dashboard/MonthlyConsumptionChart.vue'
  import StockInOutChart from '@/components/consumables/dashboard/StockInOutChart.vue'
  import InventoryByItemChart from '@/components/consumables/dashboard/InventoryByItemChart.vue'
</script>
