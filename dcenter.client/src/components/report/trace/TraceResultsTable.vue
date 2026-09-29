<template>
  <v-card flat border class="page-card trace-card">
    <v-card-item>
      <v-card-title class="d-flex align-center flex-wrap ga-2 text-subtitle-1">
        <span>Joints found</span>
        <v-chip size="small" variant="tonal">{{ traceResults.length }}{{ traceTruncated ? '+' : '' }}</v-chip>
        <span v-if="reportCount" class="text-caption text-medium-emphasis">across {{ reportCount }} report(s)</span>
        <v-spacer />
        <v-btn variant="text" size="small" prepend-icon="mdi-download" :disabled="!traceResults.length"
               @click="downloadCsv">
          Download CSV
        </v-btn>
      </v-card-title>
      <v-card-subtitle v-if="traceTruncated">
        Showing the newest {{ traceResults.length }} joints — narrow the search to see everything.
      </v-card-subtitle>
    </v-card-item>
    <v-divider />
    <div class="card-table-area">
      <v-data-table-virtual :headers="headers" :items="traceResults" :loading="loadingTrace"
                            height="100%" density="compact" fixed-header hover class="clickable-rows"
                            :item-value="(r) => `${r.workOrderNumber}#${r.jointNumber}`"
                            :no-data-text="noDataText"
                            @click:row="(_e, { item }) => open(item.workOrderNumber)">
        <template #loading><v-skeleton-loader type="table-row@6" /></template>
        <template #item.workOrderNumber="{ item }"><strong>{{ item.workOrderNumber }}</strong></template>
        <template #item.status="{ item }"><ReportStatusChip :status="item.status" /></template>
        <template #item.welder="{ item }">
          {{ item.welderName || '—' }}
          <span v-if="item.welderNo" class="text-caption text-medium-emphasis">({{ item.welderNo }})</span>
        </template>
        <template #item.heat="{ item }">{{ [item.heatNumberLeft, item.heatNumberRight].filter(Boolean).join(' / ') || '—' }}</template>
        <template #item.heatLots="{ item }">{{ item.heatLots || '—' }}</template>
      </v-data-table-virtual>
    </div>
  </v-card>
</template>

<script setup>
  import { computed } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useRouter } from 'vue-router';
  import { useReportInsightsStore } from '@/store/reportInsightsStore';
  import ReportStatusChip from '@/components/report/ReportStatusChip.vue';
  import { downloadCsv as saveCsv } from '@/utils/files';

  const router = useRouter();
  const { traceResults, traceTruncated, traceSearched, loadingTrace, traceField, traceQuery } = storeToRefs(useReportInsightsStore());

  const headers = [
    { title: 'Work Order Number', key: 'workOrderNumber', width: '150px' },
    { title: 'Part No.', key: 'partNo', width: '150px' },
    { title: 'Status', key: 'status', width: '110px' },
    { title: 'Date welded', key: 'dateWelded', width: '120px' },
    { title: 'Joint', key: 'jointNumber', width: '70px', align: 'center' },
    { title: 'WPS No.', key: 'wpsNo', width: '130px' },
    { title: 'Welder', key: 'welder', sortable: false },
    { title: 'Part heat no.', key: 'heat', sortable: false },
    { title: 'Electrode heat/lot', key: 'heatLots' },
  ];

  const reportCount = computed(() => new Set(traceResults.value.map((r) => r.workOrderNumber)).size);
  const noDataText = computed(() =>
    traceSearched.value ? 'No joints match this search.' : 'Type at least 2 characters to search.');

  function open(workOrderNumber) {
    router.push({ name: 'report-editor', params: { workOrderNumber } });
  }

  function downloadCsv() {
    const cols = ['workOrderNumber', 'partNo', 'status', 'dateWelded', 'jointNumber', 'wpsNo',
      'welderName', 'welderNo', 'heatNumberLeft', 'heatNumberRight', 'heatLots'];
    const rows = traceResults.value.map((r) => cols.map((c) => r[c]));
    saveCsv(`trace-${traceField.value}-${traceQuery.value.trim().replace(/[^\w.-]+/g, '_')}.csv`, [cols, ...rows]);
  }
</script>

<style scoped>
  .trace-card {
    height: calc(100vh - var(--viewport-chrome) - 190px);
    min-height: 360px;
  }

  .clickable-rows :deep(tbody tr) {
    cursor: pointer;
  }
</style>
