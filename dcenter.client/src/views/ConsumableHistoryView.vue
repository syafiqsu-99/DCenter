<template>
  <div>
    <StickyBar><TransactionFilters /></StickyBar>
    <TransactionTable />
  </div>
</template>

<script setup>
  import StickyBar from '@/components/StickyBar.vue'
  import TransactionFilters from '@/components/consumables/TransactionFilters.vue'
  import TransactionTable from '@/components/consumables/TransactionTable.vue'
</script>
