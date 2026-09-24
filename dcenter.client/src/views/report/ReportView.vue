<template>
  <v-alert v-if="error" type="error" variant="tonal" class="mb-4" closable>{{ error }}</v-alert>

  <WorkOrderBrowser v-if="!confirmed" />

  <template v-else>
    <ReportActions />
    <ReportForm />
  </template>
</template>

<script setup>
    import { storeToRefs } from 'pinia';
    import { useReportStore } from '@/store/reportStore';
    import WorkOrderBrowser from '@/components/report/WorkOrderBrowser.vue';
    import ReportForm from '@/components/report/ReportForm.vue';
    import ReportActions from '@/components/report/ReportActions.vue';

    const store = useReportStore();
    const { confirmed, error } = storeToRefs(store);
</script>
