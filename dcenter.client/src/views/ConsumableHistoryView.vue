<template>
  <div class="consumable-page">
    <TransactionFilters />
    <TransactionTable />
  </div>
</template>

<script setup>
  import TransactionFilters from '@/components/consumables/TransactionFilters.vue'
  import TransactionTable from '@/components/consumables/TransactionTable.vue'
</script>
