<template>
  <v-card flat border class="page-card saved-card">
    <v-card-item>
      <v-card-title class="d-flex align-center flex-wrap ga-2">
        <v-icon icon="mdi-file-document-multiple-outline" class="me-1" />
        <span>Saved reports</span>
        <v-spacer />
        <v-text-field v-model="savedSearch" prepend-inner-icon="mdi-magnify" label="Search WO, part or description"
                      variant="outlined" density="compact" hide-details clearable
                      style="max-width:280px;min-width:200px;" />
        <v-btn variant="text" size="small" prepend-icon="mdi-refresh"
               aria-label="Refresh saved reports"
               :loading="loadingSaved" @click="store.loadSavedReports().catch(() => {})">
          Refresh
        </v-btn>
      </v-card-title>
    </v-card-item>

    <v-tabs v-model="listTab" density="compact" color="primary" class="px-2">
      <v-tab v-for="t in tabs" :key="t.value" :value="t.value" class="text-none">
        {{ t.label }}
        <v-chip size="x-small" variant="tonal" class="ms-2">{{ t.count }}</v-chip>
      </v-tab>
    </v-tabs>
    <v-divider />

    <div class="card-table-area">
      <v-data-table-virtual :headers="headers" :items="visible" :loading="loadingSaved"
                            :sort-by="[{ key: 'updatedAt', order: 'desc' }]"
                            height="100%" density="compact" fixed-header hover
                            item-value="workOrderNumber" class="clickable-rows"
                            no-data-text="No saved reports match this filter."
                            @click:row="(_e, { item }) => open(item.workOrderNumber)">
        <template #loading>
          <v-skeleton-loader type="table-row@8" />
        </template>
        <template #item.workOrderNumber="{ item }">
          <strong>{{ item.workOrderNumber }}</strong>
        </template>
        <template #item.status="{ item }">
          <ReportStatusChip :status="item.status" />
        </template>
        <template #item.updatedAt="{ item }">
          {{ fmt(item.updatedAt) }}
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-file-pdf-box" size="small" variant="text" color="red-darken-2"
                 :aria-label="`View PDF for work order ${item.workOrderNumber}`" title="View PDF"
                 @click.stop="viewPdf(item)" />
          <v-menu location="bottom end">
            <template #activator="{ props }">
              <v-btn v-bind="props" icon="mdi-dots-vertical" size="small" variant="text"
                     :aria-label="`More actions for work order ${item.workOrderNumber}`" @click.stop />
            </template>
            <v-list density="compact" min-width="180">
              <v-list-item prepend-icon="mdi-folder-open-outline" title="Open"
                           @click="open(item.workOrderNumber)" />
              <v-list-item prepend-icon="mdi-content-copy" title="Duplicate"
                           :to="{ name: 'report-duplicate', params: { source: item.workOrderNumber } }" />
              <v-divider class="my-1" />
              <v-list-item prepend-icon="mdi-delete-outline" title="Delete draft" base-color="error"
                           :subtitle="supervisor.isSupervisor ? undefined : 'Supervisor only'"
                           :disabled="item.status === 'Completed' || !supervisor.isSupervisor" @click="askDelete(item)" />
            </v-list>
          </v-menu>
        </template>
      </v-data-table-virtual>
    </div>
  </v-card>

  <ReportPdfDialog v-model="pdfDialog" :work-order-number="pdfReport?.workOrderNumber ?? ''"
                   :part-no="pdfReport?.partNo ?? ''" :description="pdfReport?.description ?? ''" />

  <ConfirmDeleteDialog v-model="confirmDialog" title="Delete this draft?"
                       :loading="deletingRow" @confirm="doDelete">
    This permanently removes the draft for job
    <strong>{{ pendingDelete?.workOrderNumber }}</strong>. This cannot be undone.
  </ConfirmDeleteDialog>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useRouter } from 'vue-router';
  import { useReportStore } from '@/store/reportStore';
  import { useConsumableStore } from '@/store/consumableStore';
  import ConfirmDeleteDialog from '@/components/common/ConfirmDeleteDialog.vue';
  import ReportStatusChip from '@/components/report/ReportStatusChip.vue';
  import ReportPdfDialog from '@/components/report/ReportPdfDialog.vue';

  const store = useReportStore();
  const supervisor = useConsumableStore();
  const router = useRouter();
  const { savedReports, loadingSaved, listTab } = storeToRefs(store);
  const savedSearch = ref('');

  const headers = [
    { title: 'Work Order Number', key: 'workOrderNumber', width: '150px' },
    { title: 'Part No.', key: 'partNo', width: '160px' },
    { title: 'Description', key: 'description' },
    { title: 'Joints', key: 'jointCount', width: '90px', align: 'center' },
    { title: 'Status', key: 'status', width: '120px' },
    { title: 'Updated', key: 'updatedAt', width: '180px' },
    { title: '', key: 'actions', width: '100px', sortable: false, align: 'end' },
  ];

  const searched = computed(() => {
    const q = savedSearch.value?.trim().toLowerCase();
    if (!q) return savedReports.value;
    return savedReports.value.filter((r) =>
      (r.workOrderNumber ?? '').toLowerCase().includes(q) ||
      (r.partNo ?? '').toLowerCase().includes(q) ||
      (r.description ?? '').toLowerCase().includes(q));
  });

  const tabs = computed(() => [
    { value: 'all', label: 'All', count: searched.value.length },
    { value: 'Draft', label: 'Drafts', count: searched.value.filter((r) => r.status === 'Draft').length },
    { value: 'Completed', label: 'Completed', count: searched.value.filter((r) => r.status === 'Completed').length },
  ]);

  const visible = computed(() =>
    listTab.value === 'all' ? searched.value : searched.value.filter((r) => r.status === listTab.value));

  function open(workOrderNumber) {
    router.push({ name: 'report-editor', params: { workOrderNumber } });
  }

  const pdfDialog = ref(false);
  const pdfReport = ref(null);

  function viewPdf(item) {
    pdfReport.value = item;
    pdfDialog.value = true;
  }

  const confirmDialog = ref(false);
  const pendingDelete = ref(null);
  const deletingRow = ref(false);

  function askDelete(item) { pendingDelete.value = item; confirmDialog.value = true; }

  async function doDelete() {
    if (!pendingDelete.value) return;
    deletingRow.value = true;
    try { await store.deleteSaved(pendingDelete.value.workOrderNumber); }
    finally { deletingRow.value = false; confirmDialog.value = false; pendingDelete.value = null; }
  }

  function fmt(iso) { return iso ? new Date(iso).toLocaleString() : ''; }

  onMounted(() => {
    if (!store.savedReports.length) store.loadSavedReports().catch(() => {});
  });
</script>

<style scoped>
  .saved-card {
    height: clamp(320px, 45vh, 480px);
  }

  .saved-card > .v-tabs {
    flex: 0 0 auto;
  }

  .clickable-rows :deep(tbody tr) {
    cursor: pointer;
  }
</style>
