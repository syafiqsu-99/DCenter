<template>
  <div class="report-actions-sticky">
    <v-card flat border>
      <v-toolbar color="transparent" density="comfortable" class="flex-wrap">
        <v-btn icon="mdi-arrow-left" variant="text" aria-label="Back to job list"
               @click="store.backToList()" />

        <div class="ms-1 me-3">
          <div class="text-subtitle-2 font-weight-medium">{{ report.jobNumber }}</div>
          <div class="text-caption text-medium-emphasis">
            {{ isComplete ? 'Completed' : 'Draft' }}
          </div>
        </div>

        <v-spacer />

        <div class="d-flex align-center ga-1 py-1">
          <v-btn :loading="saving" :disabled="!hasDateWelded" variant="text"
                 prepend-icon="mdi-content-save" @click="saveOnly">
            Save
          </v-btn>

          <v-btn v-if="!isComplete" color="success" variant="flat" :disabled="!hasDateWelded"
                 prepend-icon="mdi-check" @click="markComplete">
            Complete
          </v-btn>
          <v-btn v-else color="warning" variant="tonal"
                 prepend-icon="mdi-lock-open-variant-outline" @click="reopen">
            Reopen
          </v-btn>

          <v-menu location="bottom end">
            <template #activator="{ props }">
              <v-btn v-bind="props" icon="mdi-dots-vertical" variant="text"
                     aria-label="More report actions" />
            </template>
            <v-list density="compact" min-width="220">
              <v-list-item prepend-icon="mdi-file-pdf-box" title="View PDF"
                           :disabled="pdfLoading" @click="viewPdf" />
              <v-list-item prepend-icon="mdi-microsoft-excel" title="Download Excel"
                           :disabled="excelLoading" @click="downloadExcel" />
              <v-list-item prepend-icon="mdi-history" title="Status history"
                           @click="openHistory" />
              <v-divider class="my-1" />
              <v-list-item prepend-icon="mdi-delete-outline" title="Delete draft"
                           base-color="error" :disabled="isComplete"
                           @click="confirmDelete = true" />
            </v-list>
          </v-menu>
        </div>
      </v-toolbar>

      <v-alert v-if="!hasDateWelded" type="warning" variant="tonal" density="compact" tile
               icon="mdi-calendar-alert">
        Date welded is required before this report can be saved or marked complete.
      </v-alert>

      <v-alert v-if="conflict" type="error" variant="tonal" density="compact" tile
               icon="mdi-account-alert-outline">
        {{ error }}
        <template #append>
          <v-btn size="small" variant="text" @click="store.reloadReport()">Reload</v-btn>
        </template>
      </v-alert>
    </v-card>
  </div>

  <ConfirmDeleteDialog v-model="confirmDelete" title="Delete this draft?"
                       :loading="deleting" @confirm="doDelete">
    This permanently removes the draft for job <strong>{{ report.jobNumber }}</strong>.
    This cannot be undone.
  </ConfirmDeleteDialog>

  <v-dialog v-model="historyDialog" max-width="480">
    <v-card>
      <v-card-title>Status history</v-card-title>
      <v-card-text>
        <v-skeleton-loader v-if="loadingHistory" type="list-item-two-line@3" />
        <div v-else-if="!history.length" class="text-medium-emphasis">
          This report has not been completed or reopened yet.
        </div>
        <v-timeline v-else side="end" density="compact" align="start">
          <v-timeline-item v-for="(h, i) in history" :key="i"
                           :dot-color="h.action === 'Completed' ? 'success' : 'warning'"
                           size="x-small">
            <div class="text-body-2 font-weight-medium">{{ h.action }}</div>
            <div class="text-caption text-medium-emphasis">{{ fmt(h.occurredAt) }}</div>
          </v-timeline-item>
        </v-timeline>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="historyDialog = false">Close</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="3000">{{ snackbarText }}</v-snackbar>
</template>

<script setup>
      import { ref } from 'vue';
      import { storeToRefs } from 'pinia';
      import { useReportStore } from '@/store/reportStore';
      import api from '@/utils/api';
      import ConfirmDeleteDialog from '@/components/ConfirmDeleteDialog.vue';

      const store = useReportStore();
      const { report, saving, deleting, hasDateWelded, isComplete, conflict, error,
              history, loadingHistory } = storeToRefs(store);

      const snackbar = ref(false);
      const snackbarText = ref('');
      const snackbarColor = ref('success');
      const confirmDelete = ref(false);
      const historyDialog = ref(false);
      const pdfLoading = ref(false);
      const excelLoading = ref(false);

      function notify(text, color = 'success') {
        snackbarText.value = text;
        snackbarColor.value = color;
        snackbar.value = true;
      }

      function fmt(iso) {
        return iso ? new Date(iso).toLocaleString() : '';
      }

      async function saveOnly() {
        if (await store.save()) notify('Draft saved.');
        else notify(store.error || 'Save failed.', 'error');
      }

      async function markComplete() {
        if (!(await store.save())) {
          notify(store.error || 'Save failed.', 'error');
          return;
        }
        if (await store.setComplete(true)) notify('Report marked complete.');
        else notify(store.error || 'Could not mark complete.', 'error');
      }

      async function reopen() {
        if (await store.setComplete(false)) notify('Report reopened as draft.');
        else notify(store.error || 'Could not reopen report.', 'error');
      }

      async function openHistory() {
        historyDialog.value = true;
        await store.loadHistory();
      }

      async function doDelete() {
        const ok = await store.deleteDraft();
        confirmDelete.value = false;
        if (!ok) notify(store.error || 'Could not delete the draft.', 'error');
      }

      async function viewPdf() {
        if (!(await store.save())) {
          notify(store.error || 'Save failed.', 'error');
          return;
        }
        pdfLoading.value = true;
        try {
          const res = await api.get(`/reports/${encodeURIComponent(report.value.jobNumber)}/pdf`, {
            responseType: 'blob',
          });
          window.open(URL.createObjectURL(res.data), '_blank');
        } finally {
          pdfLoading.value = false;
        }
      }

      async function downloadExcel() {
        if (!(await store.save())) {
          notify(store.error || 'Save failed.', 'error');
          return;
        }
        excelLoading.value = true;
        try {
          const res = await api.get(`/reports/${encodeURIComponent(report.value.jobNumber)}/excel`, {
            responseType: 'blob',
          });
          const url = URL.createObjectURL(res.data);
          const a = document.createElement('a');
          a.href = url;
          a.download = `WeldOrderCard_${report.value.jobNumber}.xlsx`;
          a.click();
          URL.revokeObjectURL(url);
        } finally {
          excelLoading.value = false;
        }
      }
</script>

<style scoped>
  .report-actions-sticky {
    position: sticky;
    top: calc(var(--v-layout-top, 64px) + 8px);
    z-index: 5;
    margin-bottom: 16px;
  }

    .report-actions-sticky > .v-card {
      background: rgb(var(--v-theme-surface));
      box-shadow: 0 2px 10px rgba(0, 0, 0, 0.12);
    }
</style>
