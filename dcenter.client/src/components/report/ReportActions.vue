<template>
  <div class="report-actions-sticky">
    <v-card flat border>
      <v-toolbar color="transparent" density="comfortable" class="flex-wrap">
        <v-btn icon="mdi-arrow-left" variant="text" aria-label="Back to work order list"
               @click="attemptBack" />

        <div class="ms-1 me-3">
          <div class="text-subtitle-2 font-weight-medium">{{ report.workOrderNumber || 'New report (duplicate)' }}</div>
          <div class="text-caption text-medium-emphasis">
            {{ isComplete ? REPORT_STATUS.completed : REPORT_STATUS.draft }}<span v-if="isDirty && !isComplete" class="text-warning"> · Unsaved changes</span>
          </div>
        </div>

        <v-spacer />

        <div class="d-flex align-center ga-1 py-1">
          <v-btn v-if="!isComplete" :loading="saving" :disabled="!hasDateWelded || completing" variant="text"
                 prepend-icon="mdi-content-save" @click="saveOnly">
            Save
          </v-btn>

          <v-btn v-if="!isComplete" color="success" variant="flat" :disabled="!hasDateWelded || saving"
                 :loading="completing" prepend-icon="mdi-check" @click="markComplete">
            Complete
          </v-btn>
          <v-btn v-else-if="supervisor.isSupervisor" color="warning" variant="tonal" :loading="completing"
                 prepend-icon="mdi-lock-open-variant-outline" @click="reopen">
            Reopen
          </v-btn>
          <v-chip v-else size="small" variant="tonal" prepend-icon="mdi-lock-outline"
                  title="Ask a supervisor to reopen this report if it needs changes.">
            Locked
          </v-chip>

          <v-menu location="bottom end">
            <template #activator="{ props }">
              <v-btn v-bind="props" icon="mdi-dots-vertical" variant="text"
                     aria-label="More report actions" />
            </template>
            <v-list density="compact" min-width="220">
              <v-list-item prepend-icon="mdi-file-pdf-box" title="View PDF"
                           @click="viewPdf" />
              <v-list-item prepend-icon="mdi-microsoft-excel" title="Download Excel"
                           :disabled="excelLoading" @click="downloadExcel" />
              <v-list-item prepend-icon="mdi-history" title="Status history"
                           @click="openHistory" />
              <v-divider class="my-1" />
              <v-list-item prepend-icon="mdi-delete-outline" title="Delete draft"
                           :subtitle="supervisor.isSupervisor ? undefined : 'Supervisor only'"
                           base-color="error" :disabled="isComplete || !supervisor.isSupervisor"
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
    This permanently removes the draft for job <strong>{{ report.workOrderNumber }}</strong>.
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
        <v-timeline-item v-for="(h, i) in history" :key="i"
                         :dot-color="actionColor(h.action)"
                         size="x-small">
          <div class="text-body-2 font-weight-medium">{{ h.action }}</div>
          <div v-if="h.details" class="text-caption">{{ h.details }}</div>
          <div class="text-caption text-medium-emphasis">{{ fmt(h.occurredAt) }}</div>
        </v-timeline-item>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="historyDialog = false">Close</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <ReportPdfDialog v-model="pdfDialog" :work-order-number="report.workOrderNumber"
                   :part-no="report.partNo" :description="report.description" />

  <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="3000">{{ snackbarText }}</v-snackbar>
</template>

<script setup>
  import { ref } from 'vue'
  import { storeToRefs } from 'pinia'
  import { useRouter } from 'vue-router'
  import { useReportStore } from '@/store/reportStore'
  import { useSupervisorStore } from '@/store/supervisorStore'
  import { saveBlob } from '@/utils/files'
  import { reportFileName } from '@/utils/fileName'
  import ConfirmDeleteDialog from '@/components/common/ConfirmDeleteDialog.vue'
  import ReportPdfDialog from '@/components/report/ReportPdfDialog.vue'
  import { REPORT_ACTION, REPORT_STATUS } from '@/utils/constants'

  const store = useReportStore()
  const supervisor = useSupervisorStore()
  const router = useRouter()
  const { report, saving, deleting, hasDateWelded, isComplete, isDirty, conflict, error,
          history, loadingHistory } = storeToRefs(store)

  const snackbar = ref(false)
  const snackbarText = ref('')
  const snackbarColor = ref('success')
  const confirmDelete = ref(false)
  const historyDialog = ref(false)
  const excelLoading = ref(false)
  function actionColor(action) {
    if (action === REPORT_ACTION.completed) return 'success'
    if (action === REPORT_ACTION.reopened) return 'warning'
    return 'info'
  }

  const pdfDialog = ref(false)
  const completing = ref(false)

  function unsavedBlocksOutput() {
    if (!isDirty.value || isComplete.value) return false
    notify('Save your changes first — the PDF and Excel are built from the saved report.', 'warning')
    return true
  }

  function viewPdf() {
    if (unsavedBlocksOutput()) return
    pdfDialog.value = true
  }

  function attemptBack() {
    router.push({ name: 'report-list' })
  }

  function notify(text, color = 'success') {
    snackbarText.value = text
    snackbarColor.value = color
    snackbar.value = true
  }

  function fmt(iso) {
    return iso ? new Date(iso).toLocaleString() : ''
  }

  async function saveOnly() {
    if (await store.save()) notify('Draft saved.')
    else notify(store.error || 'Save failed.', 'error')
  }

  async function markComplete() {
    if (completing.value) return
    completing.value = true
    try {
      if (!(await store.save())) {
        notify(store.error || 'Save failed.', 'error')
        return
      }
      if (await store.setComplete(true)) notify('Report marked complete.')
      else notify(store.error || 'Could not mark complete.', 'error')
    } finally {
      completing.value = false
    }
  }

  async function reopen() {
    if (completing.value) return
    completing.value = true
    try {
      if (await store.setComplete(false)) notify('Report reopened as draft.')
      else notify(store.error || 'Could not reopen report.', 'error')
    } finally {
      completing.value = false
    }
  }

  async function openHistory() {
    historyDialog.value = true
    await store.loadHistory()
  }

  async function doDelete() {
    const ok = await store.deleteDraft()
    confirmDelete.value = false
    if (ok) router.push({ name: 'report-list' })
    else notify(store.error || 'Could not delete the draft.', 'error')
  }

  async function downloadExcel() {
    if (unsavedBlocksOutput()) return
    excelLoading.value = true
    try {
      const r = report.value
      const blob = await store.fetchReportFile(r.workOrderNumber, 'excel')
      saveBlob(blob, `${reportFileName(r.workOrderNumber, r.partNo, r.description)}.xlsx`)
    } catch {
      notify('Could not download the Excel file.', 'error')
    } finally {
      excelLoading.value = false
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
