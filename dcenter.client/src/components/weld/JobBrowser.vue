<template>
  <v-card flat border class="mb-4">
    <v-card-text>
      <v-row align="center" dense>
        <v-col cols="12" sm="8" md="6">
          <v-text-field v-model="searchInput"
                        label="Job number"
                        variant="outlined"
                        placeholder="Type to filter the list"
                        prepend-inner-icon="mdi-magnify"
                        hide-details
                        clearable />
        </v-col>
        <v-col cols="12" sm="4" md="3">
          <v-btn v-if="canSelect"
                 color="primary"
                 block
                 :loading="loading"
                 prepend-icon="mdi-check"
                 @click="store.selectJob()">
            Select {{ distinctJobs[0] }}
          </v-btn>
        </v-col>
      </v-row>
    </v-card-text>

    <v-divider />

    <v-data-table-virtual :headers="headers"
                          :items="filteredRows"
                          :loading="loadingRows"
                          height="560"
                          density="compact"
                          fixed-header
                          item-value="_index" />
  </v-card>

  <!-- Saved drafts / completed reports -->
  <v-card flat border>
    <v-card-title class="d-flex align-center">
      <span>Saved reports</span>
      <v-spacer />
      <v-btn variant="text" size="small" prepend-icon="mdi-refresh"
             :loading="loadingSaved" @click="store.loadSavedReports()">
        Refresh
      </v-btn>
    </v-card-title>

    <v-data-table-virtual :headers="savedHeaders"
                          :items="savedReports"
                          :loading="loadingSaved"
                          density="compact"
                          no-data-text="No saved reports yet.">
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
      </template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
    import { onMounted } from 'vue';
    import { storeToRefs } from 'pinia';
    import { useReportStore } from '@/store/reportStore';

    const store = useReportStore();
    const { searchInput, filteredRows, canSelect, distinctJobs, loadingRows, loading,
            savedReports, loadingSaved } = storeToRefs(store);

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

    function fmt(iso) {
          return iso ? new Date(iso).toLocaleString() : '';
    }

    onMounted(() => {
          if (!store.allRows.length) store.loadRows().catch(() => {});
          store.loadSavedReports().catch(() => {});
    });
</script>
