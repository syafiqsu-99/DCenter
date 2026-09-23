<template>
  <div>
    <TransactionFilters class="mb-3" />
    <TransactionTable />
  </div>
</template>

<script setup>
  import TransactionFilters from '@/components/consumables/TransactionFilters.vue'
  import TransactionTable from '@/components/consumables/TransactionTable.vue'
</script>
