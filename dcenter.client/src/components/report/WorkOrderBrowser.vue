<template>
  <v-card flat border class="page-card">
    <v-card-item>
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-magnify" class="me-2" />
        Find a work order
      </v-card-title>
      <v-card-subtitle>Search by work order number, then press Enter or use the button to open its report.</v-card-subtitle>
    </v-card-item>

    <v-card-text>
      <v-row align="center" dense>
        <v-col cols="12" md="7" lg="6">
          <v-text-field :model-value="searchInput"
                        label="Work order number"
                        variant="outlined"
                        placeholder="Type to filter (2+ characters)"
                        prepend-inner-icon="mdi-file-search-outline"
                        hide-details
                        clearable
                        autofocus
                        @update:model-value="store.setSearchInput($event ?? '')"
                        @click:clear="store.setSearchInput('')"
                        @keydown.enter="openTarget">
            <template v-if="targetWorkOrder" #append-inner>
              <v-btn color="primary" variant="flat" size="small" class="text-none ms-2"
                     :prepend-icon="targetAction.icon" @click.stop="openTarget">
                {{ targetAction.label }} {{ targetWorkOrder }}
              </v-btn>
            </template>
          </v-text-field>
        </v-col>
        <v-col cols="12" md="5" lg="6" class="text-medium-emphasis text-body-2">
          <template v-if="searchQuery">
            <span v-if="filteredRows.length === 0 && !loadingRows">No work orders match "{{ searchQuery }}".</span>
            <span v-else-if="!targetWorkOrder && distinctWorkOrders.length > 1">
              {{ distinctWorkOrders.length }} work orders match — keep typing, or open one from the table.
            </span>
          </template>
        </v-col>
      </v-row>
      <div v-if="filteredRows.length" class="text-caption text-medium-emphasis mt-1">
        Showing {{ filteredRows.length }}{{ hasMoreResults ? '+' : '' }}
        {{ searchQuery ? 'matching work orders' : 'work orders' }}<span v-if="hasMoreResults"> — scroll for more</span>.
      </div>
    </v-card-text>

    <v-divider />

    <div ref="tableArea" class="card-table-area">
      <v-data-table-virtual v-model:expanded="expanded" :headers="headers" :items="filteredRows" :loading="loadingRows"
                            height="100%" density="compact" fixed-header hover show-expand
                            item-value="workOrderNumber" :row-props="workOrderRowProps"
                            :no-data-text="noDataText"
                            @click:row="onRowClick">
        <template #loading>
          <v-skeleton-loader type="table-row@8" />
        </template>
        <template #item.workOrderNumber="{ item }">
          <strong>{{ item.workOrderNumber }}</strong>
        </template>
        <template #item.assemblyItem="{ item }">{{ item.assemblyItem || '—' }}</template>
        <template #item.status="{ item }">
          <ReportStatusChip :status="savedByWorkOrder.get(item.workOrderNumber)?.status" />
        </template>
        <template #item.actions="{ item }">
          <v-btn size="small" variant="text" color="primary" class="text-none"
                 :prepend-icon="actionFor(item.workOrderNumber).icon"
                 :aria-label="`${actionFor(item.workOrderNumber).label} for work order ${item.workOrderNumber}`"
                 @click.stop="open(item.workOrderNumber)">
            {{ actionFor(item.workOrderNumber).label }}
          </v-btn>
        </template>
        <template #expanded-row="{ columns, item }">
          <tr>
            <td :colspan="columns.length" class="bg-grey-lighten-5 pa-0">
              <WorkOrderTree :work-order-number="item.workOrderNumber" />
            </td>
          </tr>
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
</template>

<script setup>
  import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useRouter } from 'vue-router';
  import { useReportStore } from '@/store/reportStore';
  import WorkOrderTree from '@/components/report/WorkOrderTree.vue';
  import ReportStatusChip from '@/components/report/ReportStatusChip.vue';

  const store = useReportStore();
  const router = useRouter();
  const { searchInput, searchQuery, filteredRows, distinctWorkOrders, targetWorkOrder, loadingRows,
    loadingMoreRows, hasMoreResults, savedByWorkOrder } = storeToRefs(store);

  const tableArea = ref(null);
  let scrollEl = null;

  function onTableScroll() {
    if (!scrollEl) return;
    const remaining = scrollEl.scrollHeight - scrollEl.scrollTop - scrollEl.clientHeight;
    if (remaining < 200) store.loadMoreWorkOrders();
  }

  async function attachScroll() {
    await nextTick();
    scrollEl = tableArea.value?.querySelector('.v-table__wrapper');
    scrollEl?.addEventListener('scroll', onTableScroll, { passive: true });
  }

  const headers = [
    { title: 'Work Order Number', key: 'workOrderNumber', width: '150px', sortable: false },
    { title: 'Assembly Item', key: 'assemblyItem', sortable: false },
    { title: 'Qty', key: 'qty', width: '90px', sortable: false },
    { title: 'Report', key: 'status', width: '120px', sortable: false },
    { title: '', key: 'actions', width: '170px', sortable: false, align: 'end' },
    { title: '', key: 'data-table-expand', width: '48px' },
  ];
  const expanded = ref([]);
  watch(() => store.searchQuery, () => { expanded.value = []; });

  const selectedWorkOrderRow = ref(null);
  const workOrderRowProps = ({ item }) => ({
    class: selectedWorkOrderRow.value === item.workOrderNumber ? 'bg-blue-grey-lighten-5' : '',
  });

  const ACTIONS = {
    Draft: { label: 'Open draft', icon: 'mdi-pencil-outline' },
    Completed: { label: 'View completed', icon: 'mdi-file-check-outline' },
    none: { label: 'Start report', icon: 'mdi-plus' },
  };
  function actionFor(workOrderNumber) {
    return ACTIONS[savedByWorkOrder.value.get(workOrderNumber)?.status] ?? ACTIONS.none;
  }
  const targetAction = computed(() => actionFor(targetWorkOrder.value));

  function open(workOrderNumber) {
    router.push({ name: 'report-editor', params: { workOrderNumber } });
  }
  function openTarget() {
    if (targetWorkOrder.value) open(targetWorkOrder.value);
  }

  const noDataText = computed(() =>
    searchQuery.value ? 'No work orders match your search.' : 'No work orders found.');

  function onRowClick(_event, { item }) {
    selectedWorkOrderRow.value = item.workOrderNumber;
    store.setSearchInput(item.workOrderNumber);
  }

  onMounted(async () => {
    if (!store.searchResults.length) await store.runSearch(store.searchInput);
    await attachScroll();
  });

  onBeforeUnmount(() => scrollEl?.removeEventListener('scroll', onTableScroll));
</script>
