<template>
  <div v-if="recent.length" class="d-flex align-center flex-wrap ga-2">
    <span class="text-body-2 text-medium-emphasis d-flex align-center">
      <v-icon icon="mdi-history" size="18" class="me-1" />
      Recent:
    </span>
    <v-chip v-for="wo in recent" :key="wo" size="small" variant="outlined" color="primary"
            prepend-icon="mdi-file-document-outline"
            :to="{ name: 'report-editor', params: { workOrderNumber: wo } }">
      {{ wo }}
      <span v-if="savedByWorkOrder.get(wo)?.status" class="ms-1 text-caption text-medium-emphasis">
        · {{ savedByWorkOrder.get(wo).status }}
      </span>
    </v-chip>
  </div>
</template>

<script setup>
  import { storeToRefs } from 'pinia'
  import { useReportStore } from '@/store/reportStore'

  const { recent, savedByWorkOrder } = storeToRefs(useReportStore())
</script>
