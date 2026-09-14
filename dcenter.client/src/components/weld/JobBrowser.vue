<template>
  <v-card flat border class="mb-4">
    <v-card-item>
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-magnify" class="me-2" />
        Find a job
      </v-card-title>
      <v-card-subtitle>Search by job number, then select the job to open its report.</v-card-subtitle>
    </v-card-item>

    <v-card-text>
      <v-row align="center" dense>
        <v-col cols="12" sm="8" md="6">
          <v-text-field :model-value="searchInput"
                        label="Job number"
                        variant="outlined"
                        placeholder="Type to filter the list"
                        prepend-inner-icon="mdi-file-search-outline"
                        hide-details
                        clearable
                        autofocus
                        @update:model-value="store.setSearchInput($event ?? '')" />
        </v-col>
        <v-col cols="12" sm="4" md="3">
          <v-btn v-if="canSelect"
                 color="primary"
                 block
                 :loading="loading"
                 prepend-icon="mdi-check"
                 @click="store.selectJob()">
            Open {{ distinctJobs[0] }}
          </v-btn>
        </v-col>
        <v-col cols="12" md="3" class="text-medium-emphasis text-body-2">
          <template v-if="searchInput">
            <span v-if="distinctJobs.length === 0">No jobs match "{{ searchInput }}".</span>
            <span v-else-if="distinctJobs.length > 1">{{ distinctJobs.length }} job numbers match — narrow it down to select.</span>
            <span v-else-if="existingForSelected">
              <v-icon icon="mdi-information-outline" size="16" class="me-1" />
              Already has a {{ existingForSelected.status.toLowerCase() }} report — opening it, not a blank one.
            </span>
          </template>
        </v-col>
      </v-row>
    </v-card-text>

    <v-divider />

    <v-data-table-virtual :headers="headers"
                          :items="filteredRows"
                          :loading="loadingRows"
                          height="480"
                          density="compact"
                          fixed-header
                          hover
                          item-value="_index"
                          @click:row="onRowClick" />
  </v-card>

  <!-- Saved drafts / completed reports -->
  <v-card flat border>
    <v-card-item>
      <v-card-title class="d-flex align-center flex-wrap ga-2">
        <span>Saved reports</span>
        <v-chip size="small" variant="tonal">{{ savedReports.length }}</v-chip>
        <v-spacer />
        <v-btn-toggle v-model="statusFilter" density="compact" variant="outlined" divided mandatory>
          <v-btn value="all" size="small">All</v-btn>
          <v-btn value="Draft" size="small">Drafts</v-btn>
          <v-btn value="Completed" size="small">Completed</v-btn>
        </v-btn-toggle>
        <v-btn variant="text" size="small" prepend-icon="mdi-refresh"
               :loading="loadingSaved" @click="store.loadSavedReports()">
          Refresh
        </v-btn>
      </v-card-title>
    </v-card-item>

    <v-data-table-virtual :headers="savedHeaders"
                          :items="visibleSaved"
                          :loading="loadingSaved"
                          density="compact"
                          hover
                          no-data-text="No saved reports match this filter.">
      <template #item.status="{ item }">
        <v-chip :color="item.status === 'Completed' ? 'success' : 'warning'"
                size="small" variant="tonal">
          {{ item.status }}
        </v-chip>
      </template>
      <template #item.updatedAt="{ item }">
        {{ fmt(item.updatedAt) }}
      </template>
      <template #item.actions="{ item }">
        <v-btn size="small" variant="text" color="primary"
               prepend-icon="mdi-folder-open-outline"
               @click="store.openReport(item.jobNumber)">
          Open
        </v-btn>
        <v-btn size="small" variant="text" color="error" icon="mdi-delete-outline"
               :disabled="item.status === 'Completed'"
               @click="askDelete(item)" />
      </template>
    </v-data-table-virtual>
  </v-card>

  <v-dialog v-model="confirmDialog" max-width="420">
    <v-card v-if="pendingDelete">
      <v-card-title>Delete this draft?</v-card-title>
      <v-card-text>
        This permanently removes the draft for job <strong>{{ pendingDelete.jobNumber }}</strong>.
        This cannot be undone.
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="confirmDialog = false">Cancel</v-btn>
        <v-btn color="error" variant="flat" :loading="deletingRow" @click="doDelete">Delete</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
      import { computed, onMounted, ref } from 'vue';
      import { storeToRefs } from 'pinia';
      import { useReportStore } from '@/store/reportStore';

      const store = useReportStore();
      const { searchInput, filteredRows, canSelect, distinctJobs, loadingRows, loading,
              savedReports, loadingSaved, savedByJobNumber } = storeToRefs(store);

      const headers = [
            { title: 'Job Number',     key: 'jobNumber',     width: '5%' },
            { title: 'Assembly Item',  key: 'assemblyItem',  width: '20%' },
            { title: 'Item Desc',      key: 'itemDesc',      width: '30%' },
            { title: 'Qty',            key: 'qty',           width: '5%' },
            { title: 'Child Part',     key: 'childPart',     width: '10%' },
            { title: 'Component Desc', key: 'componentDesc', width: '30%' },
      ];

      const savedHeaders = [
            { title: 'Job Number',  key: 'jobNumber' },
            { title: 'Part No.',    key: 'partNo' },
            { title: 'Description', key: 'description' },
            { title: 'Joints',      key: 'jointCount', align: 'center' },
            { title: 'Status',      key: 'status' },
            { title: 'Updated',     key: 'updatedAt' },
            { title: '',            key: 'actions', sortable: false, align: 'end' },
      ];

      const statusFilter = ref('all');
      const visibleSaved = computed(() =>
            statusFilter.value === 'all'
                  ? savedReports.value
                  : savedReports.value.filter((r) => r.status === statusFilter.value));

      const existingForSelected = computed(() =>
            canSelect.value ? savedByJobNumber.value.get(distinctJobs.value[0]) : undefined);

      const confirmDialog = ref(false);
      const pendingDelete = ref(null);
      const deletingRow = ref(false);

      function askDelete(item) {
            pendingDelete.value = item;
            confirmDialog.value = true;
      }

      async function doDelete() {
            if (!pendingDelete.value) return;
            deletingRow.value = true;
            try {
                  await store.deleteSaved(pendingDelete.value.jobNumber);
            } finally {
                  deletingRow.value = false;
                  confirmDialog.value = false;
                  pendingDelete.value = null;
            }
      }

      function fmt(iso) {
            return iso ? new Date(iso).toLocaleString() : '';
      }

      function onRowClick(_event, { item }) {
            store.setSearchInput(item.jobNumber);
      }

      onMounted(() => {
            if (!store.allRows.length) store.loadRows().catch(() => {});
            store.loadSavedReports().catch(() => {});
      });
</script>
