<template>
  <v-autocomplete :model-value="null" :items="items" item-title="workOrderNumber" item-value="workOrderNumber"
                  :custom-filter="matches" :loading="loadingSaved"
                  label="Jump to report…" prepend-inner-icon="mdi-swap-horizontal"
                  density="compact" variant="outlined" hide-details single-line
                  no-data-text="No other saved reports"
                  class="report-switcher" aria-label="Jump to another saved report"
                  @update:model-value="go">
    <template #item="{ props: p, item }">
      <v-list-item v-bind="p" :subtitle="[item.raw.partNo, item.raw.status].filter(Boolean).join(' · ')" />
    </template>
  </v-autocomplete>
</template>

<script setup>
  import { computed, onMounted } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useRouter } from 'vue-router';
  import { useReportStore } from '@/store/reportStore';

  const store = useReportStore();
  const router = useRouter();
  const { savedReports, loadingSaved, report } = storeToRefs(store);

  const items = computed(() =>
    savedReports.value.filter((r) => r.workOrderNumber !== report.value?.workOrderNumber));

  function matches(_value, query, item) {
    const q = (query ?? '').trim().toLowerCase();
    if (!q) return true;
    const r = item.raw;
    return [r.workOrderNumber, r.partNo, r.description].some((v) => (v ?? '').toLowerCase().includes(q));
  }

  function go(workOrderNumber) {
    if (workOrderNumber) router.push({ name: 'report-editor', params: { workOrderNumber } });
  }

  onMounted(() => {
    if (!savedReports.value.length) store.loadSavedReports().catch(() => {});
  });
</script>

<style scoped>
  .report-switcher {
    width: 240px;
    flex: 0 1 240px;
  }
</style>
