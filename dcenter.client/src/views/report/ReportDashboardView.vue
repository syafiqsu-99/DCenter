<template>
  <div class="d-flex flex-column ga-3">
    <StickyBar><ReportDashboardToolbar /></StickyBar>
    <ReportKpiCards />
    <ReportNeedsActionTable />
    <v-row dense>
      <v-col cols="12" lg="6"><ReportMonthlyChart /></v-col>
      <v-col cols="12" sm="6" lg="3">
        <ReportTopTable title="Top welders" :items="dashboard?.topWelders ?? null" trace-field="welder" />
      </v-col>
      <v-col cols="12" sm="6" lg="3">
        <ReportTopTable title="Most used WPS" :items="dashboard?.topWps ?? null" trace-field="wps" />
      </v-col>
    </v-row>
  </div>
</template>

<script setup>
  import { storeToRefs } from 'pinia';
  import { useReportInsightsStore } from '@/store/reportInsightsStore';
  import StickyBar from '@/components/common/StickyBar.vue';
  import ReportDashboardToolbar from '@/components/report/dashboard/ReportDashboardToolbar.vue';
  import ReportKpiCards from '@/components/report/dashboard/ReportKpiCards.vue';
  import ReportNeedsActionTable from '@/components/report/dashboard/ReportNeedsActionTable.vue';
  import ReportMonthlyChart from '@/components/report/dashboard/ReportMonthlyChart.vue';
  import ReportTopTable from '@/components/report/dashboard/ReportTopTable.vue';

  const { dashboard } = storeToRefs(useReportInsightsStore());
</script>
