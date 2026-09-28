<template>
  <div class="d-flex flex-column ga-4">
    <RecentReportsBar />
    <SavedReportsCard />
    <WorkOrderBrowser />
  </div>
</template>

<script setup>
  import RecentReportsBar from '@/components/report/RecentReportsBar.vue';
  import SavedReportsCard from '@/components/report/SavedReportsCard.vue';
  import WorkOrderBrowser from '@/components/report/WorkOrderBrowser.vue';
</script>
