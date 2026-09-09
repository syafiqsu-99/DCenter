<script setup>
import { storeToRefs } from 'pinia';
import { useReportStore } from '@/store/reportStore';

const store = useReportStore();
const { rows, resolvedJob, rowCount, canConfirm, loading } = storeToRefs(store);

const headers = [
  { title: 'Job Number', key: 'jobNumber' },
  { title: 'Assembly Item', key: 'assemblyItem' },
  { title: 'Item Desc', key: 'itemDesc' },
  { title: 'Qty', key: 'qty' },
  { title: 'Child Part', key: 'childPart' },
  { title: 'Component Desc', key: 'componentDesc' },
];
</script>

<template>
  <v-card flat border>
    <v-card-title class="d-flex align-center">
      <span>Results — {{ rowCount }} row(s)</span>
      <v-spacer />
      <v-btn
        v-if="canConfirm"
        color="primary"
        :loading="loading"
        prepend-icon="mdi-check"
        @click="store.confirmSelection()"
      >
        Confirm {{ resolvedJob }}
      </v-btn>
      <v-chip v-else color="warning" variant="tonal">
        Multiple job numbers — refine your search
      </v-chip>
    </v-card-title>

    <v-data-table :headers="headers" :items="rows" density="comfortable" />
  </v-card>
</template>
