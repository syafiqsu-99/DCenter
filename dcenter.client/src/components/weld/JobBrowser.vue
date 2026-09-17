<template>
  <div class="d-flex flex-column ga-4">
    <v-card flat border class="page-card">
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
                          placeholder="Type to filter (2+ characters)"
                          prepend-inner-icon="mdi-file-search-outline"
                          hide-details
                          clearable
                          autofocus
                          @update:model-value="store.setSearchInput($event ?? '')"
                          @click:clear="store.setSearchInput('')" />
          </v-col>
          <v-col cols="12" sm="4" md="3">
            <v-btn v-if="canSelect" color="primary" block :loading="loading"
                   prepend-icon="mdi-check" @click="store.selectJob()">
              Open {{ distinctJobs[0] }}
            </v-btn>
          </v-col>
          <v-col cols="12" md="3" class="text-medium-emphasis text-body-2">
            <template v-if="searchQuery">
              <span v-if="filteredRows.length === 0 && !loadingRows">No jobs match "{{ searchQuery }}".</span>
              <span v-else-if="distinctJobs.length > 1">{{ distinctJobs.length }} job numbers match — narrow it down to select.</span>
              <span v-else-if="existingForSelected">
                <v-icon icon="mdi-information-outline" size="16" class="me-1" />
                Already has a {{ existingForSelected.status.toLowerCase() }} report — opening it, not a blank one.
              </span>
            </template>
          </v-col>
        </v-row>
        <div v-if="filteredRows.length" class="text-caption text-medium-emphasis mt-1">
          Showing {{ filteredRows.length }}{{ hasMoreResults ? '+' : '' }}
          {{ searchQuery ? 'matching rows' : 'rows' }}<span v-if="hasMoreResults"> — scroll for more</span>.
        </div>
      </v-card-text>

      <v-divider />

      <div ref="tableArea" class="card-table-area">
        <v-data-table-virtual :headers="headers" :items="filteredRows" :loading="loadingRows"
                              height="100%" density="compact" fixed-header hover
                              item-value="_index" :row-props="jobRowProps"
                              :no-data-text="noDataText"
                              @click:row="onRowClick">
          <template #loading>
            <v-skeleton-loader type="table-row@8" />
          </template>
          <template #tbody.append>
            <tr v-if="loadingMoreRows">
              <td :colspan="headers.length" class="text-center text-caption text-medium-emphasis py-2">
                <v-progress-circular indeterminate size="16" width="2" class="me-2" />
                Loading more…
              </td>
            </tr>
          </template>
        </v-data-table-virtual>
      </div>
    </v-card>

    <v-card flat border class="page-card">
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
                 aria-label="Refresh saved reports"
                 :loading="loadingSaved" @click="store.loadSavedReports()">
            Refresh
          </v-btn>
        </v-card-title>
      </v-card-item>

      <div class="card-table-area">
        <v-data-table-virtual :headers="savedHeaders" :items="visibleSaved" :loading="loadingSaved"
                              height="100%" density="compact" fixed-header hover
                              item-value="jobNumber" :row-props="savedRowProps"
                              no-data-text="No saved reports match this filter."
                              @click:row="(e, { item }) => selectedSaved = (selectedSaved === item.jobNumber ? null : item.jobNumber)">
          <template #loading>
            <v-skeleton-loader type="table-row@8" />
          </template>
          <template #item.status="{ item }">
            <v-chip :color="item.status === 'Completed' ? 'success' : 'warning'" size="small" variant="tonal">
              {{ item.status }}
            </v-chip>
          </template>
          <template #item.updatedAt="{ item }">
            {{ fmt(item.updatedAt) }}
          </template>
          <template #item.actions="{ item }">
            <div v-if="selectedSaved === item.jobNumber" class="d-flex justify-end ga-1">
              <v-btn size="small" variant="text" color="primary" prepend-icon="mdi-folder-open-outline"
                     :aria-label="`Open report for job ${item.jobNumber}`" @click.stop="store.openReport(item.jobNumber)">
                Open
              </v-btn>
              <v-btn size="small" variant="text" color="error" icon="mdi-delete-outline"
                     :disabled="item.status === 'Completed'"
                     :aria-label="`Delete draft for job ${item.jobNumber}`" @click.stop="askDelete(item)" />
            </div>
          </template>
        </v-data-table-virtual>
      </div>
    </v-card>
  </div>

  <ConfirmDeleteDialog v-model="confirmDialog" title="Delete this draft?"
                       :loading="deletingRow" @confirm="doDelete">
    This permanently removes the draft for job
    <strong>{{ pendingDelete?.jobNumber }}</strong>. This cannot be undone.
  </ConfirmDeleteDialog>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue';
import { storeToRefs } from 'pinia';
import { useReportStore } from '@/store/reportStore';
import ConfirmDeleteDialog from '@/components/ConfirmDeleteDialog.vue';

const store = useReportStore();
const { searchInput, searchQuery, filteredRows, canSelect, distinctJobs, loadingRows, loading,
          loadingMoreRows, hasMoreResults, savedReports, loadingSaved, savedByJobNumber } = storeToRefs(store);

const tableArea = ref(null);
let scrollEl = null;

function onTableScroll() {
      if (!scrollEl) return;
      const remaining = scrollEl.scrollHeight - scrollEl.scrollTop - scrollEl.clientHeight;
      if (remaining < 200) store.loadMoreJobs();
}

async function attachScroll() {
      await nextTick();
      scrollEl = tableArea.value?.querySelector('.v-table__wrapper');
      scrollEl?.addEventListener('scroll', onTableScroll, { passive: true });
}

const headers = [
      { title: 'Job Number',     key: 'jobNumber',     width: '110px', sortable: false },
      { title: 'Assembly Item',  key: 'assemblyItem',  width: '140px', sortable: false },
      { title: 'Item Desc',      key: 'itemDesc',      width: '320px', sortable: false },
      { title: 'Qty',            key: 'qty',           width: '70px',  sortable: false },
      { title: 'Child Part',     key: 'childPart',     width: '140px', sortable: false },
      { title: 'Component Desc', key: 'componentDesc', width: '280px', sortable: false },
      { title: 'MRN',            key: 'mrn',           width: '110px', sortable: false },
      { title: 'MRN Desc',       key: 'mrnDesc',       width: '200px', sortable: false },
];

const savedHeaders = [
      { title: 'Job Number',  key: 'jobNumber',   width: '130px' },
      { title: 'Part No.',    key: 'partNo',      width: '150px' },
      { title: 'Description', key: 'description', width: '320px' },
      { title: 'Joints',      key: 'jointCount',  width: '90px', align: 'center' },
      { title: 'Status',      key: 'status',      width: '120px' },
      { title: 'Updated',     key: 'updatedAt',   width: '180px' },
      { title: '',            key: 'actions',     width: '150px', sortable: false, align: 'end' },
];

const statusFilter = ref('all');
const selectedSaved = ref(null);
const selectedJobRow = ref(null);

const jobRowProps = ({ item }) => ({
      class: selectedJobRow.value === item._index ? 'bg-blue-grey-lighten-5' : '',
});
const savedRowProps = ({ item }) => ({
      class: selectedSaved.value === item.jobNumber ? 'bg-blue-grey-lighten-5' : '',
});

const visibleSaved = computed(() =>
      statusFilter.value === 'all' ? savedReports.value : savedReports.value.filter((r) => r.status === statusFilter.value));

const existingForSelected = computed(() =>
      canSelect.value ? savedByJobNumber.value.get(distinctJobs.value[0]) : undefined);

const noDataText = computed(() =>
      searchQuery.value ? 'No jobs match your search.' : 'No jobs found.');

const confirmDialog = ref(false);
const pendingDelete = ref(null);
const deletingRow = ref(false);

function askDelete(item) { pendingDelete.value = item; confirmDialog.value = true; }

async function doDelete() {
      if (!pendingDelete.value) return;
      deletingRow.value = true;
      try { await store.deleteSaved(pendingDelete.value.jobNumber); }
      finally { deletingRow.value = false; confirmDialog.value = false; pendingDelete.value = null; }
}

function fmt(iso) { return iso ? new Date(iso).toLocaleString() : ''; }

function onRowClick(_event, { item }) {
      selectedJobRow.value = item._index;
      store.setSearchInput(item.jobNumber);
}

onMounted(async () => {
      if (!store.searchResults.length) await store.runSearch(store.searchInput);
      await attachScroll();
      if (!store.savedReports.length) store.loadSavedReports().catch(() => {});
});

onBeforeUnmount(() => scrollEl?.removeEventListener('scroll', onTableScroll));
</script>
