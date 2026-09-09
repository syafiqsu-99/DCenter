<template>
  <v-alert v-if="error" type="error" variant="tonal" class="mb-4" closable>{{ error }}</v-alert>

  <JobBrowser v-if="!confirmed" />

  <template v-else>
    <v-btn variant="text"
           prepend-icon="mdi-arrow-left"
           class="mb-2"
           @click="store.backToList()">
      Back to list
    </v-btn>
    <ReportForm />
    <ReportActions />
  </template>
</template>

<script setup>
  import { storeToRefs } from 'pinia';
  import { useReportStore } from '@/store/reportStore';
  import JobBrowser from '@/components/weld/JobBrowser.vue';
  import ReportForm from '@/components/weld/ReportForm.vue';
  import ReportActions from '@/components/weld/ReportActions.vue';

  const store = useReportStore();
  const { confirmed, error } = storeToRefs(store);
</script>
