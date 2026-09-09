<script setup>
import { storeToRefs } from 'pinia';
import { useReportStore } from '@/store/reportStore';
import JobSearch from '@/components/weld/JobSearch.vue';
import JobResultTable from '@/components/weld/JobResultTable.vue';
import ReportForm from '@/components/weld/ReportForm.vue';
import ReportActions from '@/components/weld/ReportActions.vue';

const store = useReportStore();
const { confirmed, rows, error } = storeToRefs(store);
</script>

<template>
  <v-alert v-if="error" type="error" variant="tonal" class="mb-4" closable>{{ error }}</v-alert>

  <JobSearch />

  <template v-if="!confirmed">
    <JobResultTable v-if="rows.length" />
  </template>

  <template v-else>
    <ReportForm />
    <ReportActions />
  </template>
</template>
