<template>
  <v-card flat border class="report-actions-bar mb-6">
    <v-toolbar color="transparent" density="comfortable" class="flex-wrap py-1">
      <v-btn variant="text" prepend-icon="mdi-arrow-left" @click="store.backToList()">
        Back to list
      </v-btn>

      <v-divider vertical inset class="mx-2 d-none d-sm-flex" />

      <span class="text-subtitle-2 font-weight-medium me-2 d-none d-sm-inline">
        {{ report.jobNumber }}
      </span>
      <v-chip :color="isComplete ? 'success' : 'warning'" size="small" variant="tonal" class="me-2">
        {{ isComplete ? 'Completed' : 'Draft' }}
      </v-chip>

      <v-spacer />

      <div class="d-flex flex-wrap ga-2 justify-end py-1">
        <v-btn color="secondary" variant="tonal" :loading="saving" :disabled="!hasDateWelded"
               prepend-icon="mdi-content-save" @click="saveOnly">
          Save draft
        </v-btn>

        <v-btn v-if="!isComplete" color="success" variant="tonal" :disabled="!hasDateWelded"
               prepend-icon="mdi-check-circle" @click="markComplete">
          Mark complete
        </v-btn>
        <v-btn v-else color="warning" variant="tonal" prepend-icon="mdi-lock-open-outline"
               @click="reopen">
          Reopen
        </v-btn>

        <v-btn color="error" variant="text" :disabled="isComplete" :loading="deleting"
               prepend-icon="mdi-delete-outline" @click="confirmDelete = true">
          Delete draft
        </v-btn>

        <v-divider vertical inset class="mx-1 d-none d-sm-flex" />

        <v-btn color="primary" variant="tonal" :loading="pdfLoading"
               prepend-icon="mdi-file-pdf-box" @click="viewPdf">
          View PDF
        </v-btn>
        <v-btn color="primary" :loading="excelLoading"
               prepend-icon="mdi-microsoft-excel" @click="downloadExcel">
          Download Excel
        </v-btn>
      </div>
    </v-toolbar>

    <v-alert v-if="!hasDateWelded" type="warning" variant="tonal" density="compact" tile
             icon="mdi-calendar-alert">
      Date welded is required before this report can be saved or marked complete.
    </v-alert>
  </v-card>

  <v-dialog v-model="confirmDelete" max-width="420">
    <v-card>
      <v-card-title>Delete this draft?</v-card-title>
      <v-card-text>
        This permanently removes the draft for job <strong>{{ report.jobNumber }}</strong>.
        This cannot be undone.
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="confirmDelete = false">Cancel</v-btn>
        <v-btn color="error" variant="flat" :loading="deleting" @click="doDelete">Delete</v-btn>
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

  const store = useReportStore();
  const { report, saving, deleting, hasDateWelded, isComplete } = storeToRefs(store);

  const snackbar = ref(false);
  const snackbarText = ref('');
  const snackbarColor = ref('success');
  const confirmDelete = ref(false);
  const pdfLoading = ref(false);
  const excelLoading = ref(false);

  function notify(text, color = 'success') {
    snackbarText.value = text;
    snackbarColor.value = color;
    snackbar.value = true;
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
  /* Pinned under the app bar so actions stay reachable while the form scrolls. */
  .report-actions-bar {
    position: sticky;
    top: 0;
    z-index: 5;
    background: rgb(var(--v-theme-surface));
  }
</style>
