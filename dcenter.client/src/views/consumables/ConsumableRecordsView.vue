<template>
  <div class="consumable-page">
    <v-btn-toggle v-model="view" mandatory divided density="compact" variant="outlined" color="primary" class="align-self-start flex-shrink-0">
      <v-btn value="history" prepend-icon="mdi-history">Transactions</v-btn>
      <v-btn value="count" prepend-icon="mdi-clipboard-edit-outline">Stock count</v-btn>
      <v-btn value="past" prepend-icon="mdi-clipboard-text-clock-outline">Past counts</v-btn>
    </v-btn-toggle>
    <template v-if="view === 'history'">
      <TransactionFilters />
      <TransactionTable />
    </template>
    <StockCountSheet v-else-if="view === 'count'" @posted="view = 'past'" />
    <StockCountHistory v-else />
  </div>
</template>

<script setup>
  import { useSubView } from '@/composables/useSubView'
  import TransactionFilters from '@/components/consumables/history/TransactionFilters.vue'
  import TransactionTable from '@/components/consumables/history/TransactionTable.vue'
  import StockCountSheet from '@/components/consumables/stock-audit/StockCountSheet.vue'
  import StockCountHistory from '@/components/consumables/stock-audit/StockCountHistory.vue'

  const view = useSubView(['history', 'count', 'past'])
</script>
