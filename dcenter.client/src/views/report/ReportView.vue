<template>
  <div>
    <v-alert v-if="error" type="error" variant="tonal" class="mb-4" closable>{{ error }}</v-alert>

    <template v-if="confirmed">
      <ReportActions />
      <ReportForm />
    </template>
    <v-skeleton-loader v-else-if="loading" type="heading, card, table-row@6" class="bg-transparent" />
    <WorkOrderBrowser v-else />
  </div>
</template>

<script setup>
    import { storeToRefs } from 'pinia';
    import { useReportStore } from '@/store/reportStore';
    import WorkOrderBrowser from '@/components/report/WorkOrderBrowser.vue';
    import ReportForm from '@/components/report/ReportForm.vue';
    import ReportActions from '@/components/report/ReportActions.vue';

    const store = useReportStore();
    const { confirmed, error, loading } = storeToRefs(store);
</script>
