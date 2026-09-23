<template>
  <div>
    <DashboardToolbar class="mb-4" />
    <KpiCards class="mb-4" />
    <v-row>
      <v-col v-for="type in store.catalog.types" :key="type" cols="12" xl="6">
        <ConsumableBalanceChart :type="type" />
      </v-col>
      <v-col cols="12" lg="6"><StockInOutChart /></v-col>
      <v-col cols="12" lg="6"><MonthlyConsumptionChart /></v-col>
      <v-col cols="12" lg="6"><LowStockTable /></v-col>
      <v-col cols="12" lg="6"><RecentTransactionsTable /></v-col>
      <v-col cols="12"><BalanceTable /></v-col>
    </v-row>
  </div>
</template>

<script setup>
  import { useConsumableStore } from '@/store/consumableStore'
  import DashboardToolbar from '@/components/consumables/DashboardToolbar.vue'
  import KpiCards from '@/components/consumables/KpiCards.vue'
  import ConsumableBalanceChart from '@/components/consumables/ConsumableBalanceChart.vue'
  import StockInOutChart from '@/components/consumables/StockInOutChart.vue'
  import MonthlyConsumptionChart from '@/components/consumables/MonthlyConsumptionChart.vue'
  import LowStockTable from '@/components/consumables/LowStockTable.vue'
  import RecentTransactionsTable from '@/components/consumables/RecentTransactionsTable.vue'
  import BalanceTable from '@/components/consumables/BalanceTable.vue'

  const store = useConsumableStore()
</script>
