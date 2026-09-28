<template>
  <div>
    <template v-if="confirmed && report">
      <ReportActions />
      <ReportForm />
    </template>
    <v-skeleton-loader v-else-if="loading" type="heading, card, table-row@6" class="bg-transparent" />
    <div v-else class="text-center py-8">
      <v-btn variant="tonal" prepend-icon="mdi-arrow-left" :to="{ name: 'report-list' }">Back to reports</v-btn>
    </div>
  </div>
</template>

<script setup>
  import { watch } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useRoute, useRouter } from 'vue-router';
  import { useReportStore } from '@/store/reportStore';
  import ReportForm from '@/components/report/ReportForm.vue';
  import ReportActions from '@/components/report/ReportActions.vue';

  const route = useRoute();
  const router = useRouter();
  const store = useReportStore();
  const { confirmed, report, loading, mode } = storeToRefs(store);

  watch(
    () => [route.name, route.params.workOrderNumber, route.params.source],
    ([name, wo, source]) => {
      if (name === 'report-editor') store.loadForWorkOrder(wo);
      else if (name === 'report-duplicate' && !(confirmed.value && mode.value === 'duplicate')) store.duplicateReport(source);
    },
    { immediate: true },
  );

  watch(
    () => [mode.value, report.value?.workOrderNumber],
    ([m, wo]) => {
      if (route.name === 'report-duplicate' && m === 'saved' && wo) {
        router.replace({ name: 'report-editor', params: { workOrderNumber: wo } });
      }
    },
  );
</script>
