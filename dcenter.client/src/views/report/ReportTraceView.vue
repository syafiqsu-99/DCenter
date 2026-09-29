<template>
  <div class="d-flex flex-column ga-4">
    <TraceSearchBar />
    <TraceResultsTable />
  </div>
</template>

<script setup>
  import TraceSearchBar from '@/components/report/trace/TraceSearchBar.vue'
  import TraceResultsTable from '@/components/report/trace/TraceResultsTable.vue'
</script>
