<template>
  <v-card flat class="settings-panel-card">
    <v-card-title class="d-flex align-center flex-wrap ga-2">
      {{ title }}
      <v-spacer />
      <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search"
                    variant="outlined" density="compact" hide-details clearable
                    style="max-width:220px;" />
      <v-btn variant="text" size="small" prepend-icon="mdi-download" @click="exportCsv">Export CSV</v-btn>
      <v-btn variant="text" size="small" prepend-icon="mdi-upload" :loading="importing"
             @click="fileInput?.click()">Import CSV</v-btn>
      <input ref="fileInput" type="file" accept=".csv" hidden @change="importCsv" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openNew">Add</v-btn>
    </v-card-title>

    <v-alert v-if="importMsg" type="info" variant="tonal" density="compact" class="mx-4"
             closable @click:close="importMsg = ''">{{ importMsg }}</v-alert>

    <div class="card-table-area">
      <v-data-table-virtual :headers="headers" :items="filteredItems" :loading="loading"
                            height="100%" density="comfortable" fixed-header hover
                            item-value="id" :row-props="rowProps"
                            no-data-text="No rows match your search."
                            @click:row="(e, { item }) => selectedId = (selectedId === item.id ? null : item.id)">
        <template #loading>
          <v-skeleton-loader type="table-row@6" />
        </template>
        <template #item.actions="{ item }">
          <div v-if="selectedId === item.id" class="d-flex justify-end ga-1">
            <v-btn icon="mdi-pencil" variant="text" size="small"
                   :aria-label="`Edit ${item[fields[0].key]}`" @click.stop="openEdit(item)" />
            <v-btn icon="mdi-delete" variant="text" size="small" color="error"
                   :aria-label="`Delete ${item[fields[0].key]}`" @click.stop="askDelete(item)" />
          </div>
        </template>
      </v-data-table-virtual>
    </div>

    <ConfirmDeleteDialog v-model="confirmDelete" :title="`Delete this ${title} row?`"
                         :item-label="pendingDelete?.[fields[0].key] ?? ''"
                         :loading="deleting" @confirm="doDelete" />

    <v-dialog v-model="dialog" :max-width="fields.length > 5 ? 860 : 520" scrollable>
      <v-card :title="`${editing?.id ? 'Edit' : 'Add'} ${title}`"
              :prepend-icon="editing?.id ? 'mdi-table-edit' : 'mdi-table-plus'">
        <v-divider />
        <v-card-text>
          <v-row dense>
            <v-col v-for="f in fields" :key="f.key"
                   cols="12" :sm="fields.length > 5 ? 6 : 12">
              <v-text-field v-model="editing[f.key]" :label="f.label"
                            variant="outlined" density="comfortable" hide-details="auto" />
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
  import { computed, onMounted, ref, useTemplateRef } from 'vue';
  import api from '@/utils/api';
  import ConfirmDeleteDialog from '@/components/common/ConfirmDeleteDialog.vue';

  const props = defineProps({
    title: { type: String, required: true },
    apiBase: { type: String, required: true },
    fields: { type: Array, required: true },
  });

  const items = ref([]);
  const search = ref('');
  const loading = ref(false);
  const saving = ref(false);
  const importing = ref(false);
  const importMsg = ref('');
  const dialog = ref(false);
  const editing = ref(null);
  const fileInput = useTemplateRef('fileInput');
  const selectedId = ref(null);
  const confirmDelete = ref(false);
  const pendingDelete = ref(null);
  const deleting = ref(false);

  const headers = computed(() => [
    ...props.fields.map((f) => ({ title: f.label, key: f.key, width: f.width ?? '180px' })),
    { title: '', key: 'actions', width: '120px', sortable: false, align: 'end' },
  ]);

  const rowProps = ({ item }) => ({
    class: selectedId.value === item.id ? 'bg-blue-grey-lighten-5' : '',
  });

  const filteredItems = computed(() => {
    const q = search.value?.trim().toLowerCase();
    if (!q) return items.value;
    return items.value.filter((row) =>
      props.fields.some((f) => (row[f.key] ?? '').toString().toLowerCase().includes(q)));
  });

  function blank() {
    return Object.fromEntries([['id', 0], ...props.fields.map((f) => [f.key, ''])]);
  }

  async function load() {
    loading.value = true;
    try {
      const { data } = await api.get(props.apiBase);
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
      if (editing.value.id) await api.put(`${props.apiBase}/${editing.value.id}`, editing.value);
      else await api.post(props.apiBase, editing.value);
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
      await api.delete(`${props.apiBase}/${pendingDelete.value.id}`);
      await load();
    } finally {
      deleting.value = false;
      confirmDelete.value = false;
      pendingDelete.value = null;
    }
  }

  function exportCsv() {
    window.open(`${api.defaults.baseURL}${props.apiBase}/export`, '_blank');
  }

  async function importCsv(e) {
    const file = e.target.files?.[0];
    if (!file) return;
    importing.value = true;
    importMsg.value = '';
    try {
      const form = new FormData();
      form.append('file', file);
      const { data } = await api.post(`${props.apiBase}/import`, form);
      importMsg.value = `Import complete — ${data.added} added, ${data.updated} updated, ${data.skipped} skipped.`;
      await load();
    } catch {
      importMsg.value = 'Import failed. Check the CSV column order.';
    } finally {
      importing.value = false;
      e.target.value = '';
    }
  }

  onMounted(load);
</script>
