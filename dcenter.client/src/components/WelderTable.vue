<template>
  <v-card flat class="settings-panel-card">
    <v-card-title class="d-flex align-center flex-wrap ga-2">
      Welders
      <v-spacer />
      <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search"
                    variant="outlined" density="compact" hide-details clearable
                    style="max-width:220px;" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openNew">Add welder</v-btn>
    </v-card-title>

    <div class="card-table-area">
      <v-data-table-virtual :headers="headers" :items="filteredItems" :loading="loading"
                            height="100%" density="comfortable" fixed-header hover
                            item-value="id" :row-props="rowProps"
                            no-data-text="No welders match your search."
                            @click:row="(e, { item }) => selectedId = (selectedId === item.id ? null : item.id)">
        <template #loading>
          <v-skeleton-loader type="table-row@6" />
        </template>
        <template #item.isActive="{ item }">
          <v-icon :color="item.isActive ? 'success' : 'grey'">
            {{ item.isActive ? 'mdi-check-circle' : 'mdi-circle-outline' }}
          </v-icon>
        </template>
        <template #item.actions="{ item }">
          <div v-if="selectedId === item.id" class="d-flex justify-end ga-1">
            <v-btn icon="mdi-pencil" variant="text" size="small"
                   :aria-label="`Edit welder ${item.welderName}`" @click.stop="openEdit(item)" />
            <v-btn icon="mdi-delete" variant="text" size="small" color="error"
                   :aria-label="`Delete welder ${item.welderName}`" @click.stop="askDelete(item)" />
          </div>
        </template>
      </v-data-table-virtual>
    </div>

    <ConfirmDeleteDialog v-model="confirmDelete" title="Delete this welder?"
                         :item-label="pendingDelete?.welderName ?? ''"
                         :loading="deleting" @confirm="doDelete" />

    <v-dialog v-model="dialog" max-width="480">
      <v-card :title="editing.id ? 'Edit welder' : 'Add welder'"
              :prepend-icon="editing.id ? 'mdi-account-edit' : 'mdi-account-plus'">
        <v-divider />
        <v-card-text>
          <v-row dense>
            <v-col cols="12" sm="7">
              <v-text-field v-model="editing.welderName" label="Welder Name"
                            variant="outlined" density="comfortable" hide-details="auto" autofocus />
            </v-col>
            <v-col cols="12" sm="5">
              <v-text-field v-model="editing.welderNo" label="Welder No."
                            variant="outlined" density="comfortable" hide-details="auto" />
            </v-col>
            <v-col cols="12">
              <v-switch v-model="editing.isActive" label="Active" color="primary" inset
                        density="comfortable" hide-details />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="px-4 py-3">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="saving" @click="save">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-card>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue';
  import api from '@/utils/api';
  import ConfirmDeleteDialog from '@/components/ConfirmDeleteDialog.vue';

  const items = ref([]);
  const search = ref('');
  const loading = ref(false);
  const saving = ref(false);
  const dialog = ref(false);
  const editing = ref(blank());
  const selectedId = ref(null);
  const confirmDelete = ref(false);
  const pendingDelete = ref(null);
  const deleting = ref(false);

  const headers = [
      { title: 'Welder Name', key: 'welderName',  width: '50%' },
      { title: 'Welder No.',  key: 'welderNo',    width: '30%' },
      { title: 'Active',      key: 'isActive',    width: '10%' },
      { title: '',            key: 'actions',     width: '10%', sortable: false, align: 'end' },
  ];

  const rowProps = ({ item }) => ({
      class: selectedId.value === item.id ? 'bg-blue-grey-lighten-5' : '',
  });

  const filteredItems = computed(() => {
      const q = search.value?.trim().toLowerCase();
      if (!q) return items.value;
      return items.value.filter((w) => w.welderName.toLowerCase().includes(q) || w.welderNo.toLowerCase().includes(q));
  });

  function blank() {
      return { id: 0, welderName: '', welderNo: '', isActive: true };
  }

  async function load() {
      loading.value = true;
      try {
        const { data } = await api.get('/welders');
        items.value = data;
      } finally {
        loading.value = false;
      }
  }

  function openNew() {
      editing.value = blank();
      dialog.value = true;
  }

  function openEdit(row) {
      editing.value = { ...row };
      dialog.value = true;
  }

  async function save() {
      saving.value = true;
      try {
        if (editing.value.id) await api.put(`/welders/${editing.value.id}`, editing.value);
        else await api.post('/welders', editing.value);
        dialog.value = false;
        await load();
      } finally {
        saving.value = false;
      }
  }

  function askDelete(row) {
      pendingDelete.value = row;
      confirmDelete.value = true;
  }

  async function doDelete() {
      if (!pendingDelete.value) return;
      deleting.value = true;
      try {
        await api.delete(`/welders/${pendingDelete.value.id}`);
        await load();
      } finally {
        deleting.value = false;
        confirmDelete.value = false;
        pendingDelete.value = null;
      }
  }

  onMounted(load);
</script>
