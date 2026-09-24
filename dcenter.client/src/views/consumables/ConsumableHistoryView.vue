<template>
  <div class="consumable-page">
    <TransactionFilters />
    <TransactionTable />
  </div>
</template>

<script setup>
  import TransactionFilters from '@/components/consumables/history/TransactionFilters.vue'
  import TransactionTable from '@/components/consumables/history/TransactionTable.vue'
</script>
