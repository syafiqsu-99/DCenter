<template>
  <v-card border flat>
    <v-card-title class="text-subtitle-1 d-flex align-center ga-2">
      <v-icon icon="mdi-alert-circle-outline" color="warning" />
      Needs action
      <v-chip size="small" variant="tonal">{{ rows.length }}</v-chip>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" :loading="loadingDashboard && !dashboard"
                          item-value="workOrderNumber" density="compact" hover fixed-header class="clickable-rows virtual-capped"
                          no-data-text="Nothing needs attention — all drafts are up to date."
                          @click:row="(_e, { item }) => open(item.workOrderNumber)">
      <template #loading><v-skeleton-loader type="table-row@4" /></template>
      <template #item.workOrderNumber="{ item }"><strong>{{ item.workOrderNumber }}</strong></template>
      <template #item.reason="{ item }">
        <v-chip size="small" variant="tonal" :color="item.dateWelded ? 'deep-orange' : 'error'">{{ item.reason }}</v-chip>
      </template>
      <template #item.updatedAt="{ item }">{{ new Date(item.updatedAt).toLocaleString() }}</template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import { computed } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useRouter } from 'vue-router';
  import { useReportInsightsStore } from '@/store/reportInsightsStore';

  const router = useRouter();
  const { dashboard, loadingDashboard } = storeToRefs(useReportInsightsStore());
  const rows = computed(() => dashboard.value?.needsAction ?? []);

  const headers = [
    { title: 'Work Order Number', key: 'workOrderNumber', width: '150px' },
    { title: 'Part No.', key: 'partNo', width: '160px' },
    { title: 'Description', key: 'description' },
    { title: 'Joints', key: 'jointCount', width: '80px', align: 'center' },
    { title: 'Reason', key: 'reason', width: '230px' },
    { title: 'Last updated', key: 'updatedAt', width: '180px' },
  ];

  function open(workOrderNumber) {
    router.push({ name: 'report-editor', params: { workOrderNumber } });
  }
</script>

<style scoped>
  .clickable-rows :deep(tbody tr) {
    cursor: pointer;
  }
</style>
