<template>
  <v-card flat border>
    <v-card-title class="d-flex align-center">
      WPS Numbers
      <v-spacer />
      <v-btn variant="text" size="small" prepend-icon="mdi-download" class="me-2"
             @click="exportCsv">Export CSV</v-btn>
      <v-btn variant="text" size="small" prepend-icon="mdi-upload" class="me-2"
             :loading="importing" @click="fileInput?.click()">Import CSV</v-btn>
      <input ref="fileInput" type="file" accept=".csv" hidden @change="importCsv" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openNew">Add WPS</v-btn>
    </v-card-title>

    <v-alert v-if="importMsg" type="info" variant="tonal" density="compact" class="mx-4"
             closable @click:close="importMsg = ''">{{ importMsg }}</v-alert>

    <v-data-table-virtual :headers="headers" :items="items" :loading="loading" density="comfortable">
      <template #item.isActive="{ item }">
        <v-icon :color="item.isActive ? 'success' : 'grey'">
          {{ item.isActive ? 'mdi-check-circle' : 'mdi-circle-outline' }}
        </v-icon>
      </template>
      <template #item.actions="{ item }">
        <v-btn icon="mdi-pencil" variant="text" size="small" @click="openEdit(item)" />
        <v-btn icon="mdi-delete" variant="text" size="small" color="error" @click="remove(item)" />
      </template>
    </v-data-table-virtual>

    <v-dialog v-model="dialog" max-width="460">
      <v-card>
        <v-card-title>{{ editing.id ? 'Edit WPS' : 'Add WPS' }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="editing.wpsNo" label="WPS No." />
          <v-text-field v-model="editing.rev" label="Rev" />
          <v-text-field v-model="editing.description" label="Description" />
          <v-switch v-model="editing.isActive" label="Active" color="primary" inset />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancel</v-btn>
          <v-btn color="primary" @click="save">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-card>
</template>

<script setup>
  import { onMounted, ref, useTemplateRef } from 'vue';
  import api from '@/utils/api';

  const items = ref([]);
  const loading = ref(false);
  const importing = ref(false);
  const importMsg = ref('');
  const dialog = ref(false);
  const editing = ref(null);
  const fileInput = useTemplateRef('fileInput');

  const headers = [
    { title: 'WPS No.', key: 'wpsNo' },
    { title: 'Rev', key: 'rev' },
    { title: 'Description', key: 'description' },
    { title: 'Active', key: 'isActive' },
    { title: '', key: 'actions', sortable: false, align: 'end' },
  ];

  function blank() {
    return { id: 0, wpsNo: '', rev: '', description: '', isActive: true };
  }

  async function load() {
    loading.value = true;
    try {
      const { data } = await api.get('/wps');
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
    const payload = {
      wpsNo: editing.value.wpsNo,
      rev: editing.value.rev,
      description: editing.value.description,
      isActive: editing.value.isActive,
    };
    if (editing.value.id) {
      await api.put(`/wps/${editing.value.id}`, payload);
    } else {
      await api.post('/wps', payload);
    }
    dialog.value = false;
    await load();
  }

  async function remove(row) {
    await api.delete(`/wps/${row.id}`);
    await load();
  }

  function exportCsv() {
    // Direct browser download from the export endpoint.
    window.open(`${api.defaults.baseURL}/wps/export`, '_blank');
  }

  async function importCsv(e) {
    const file = e.target.files?.[0];
    if (!file) return;
    importing.value = true;
    importMsg.value = '';
    try {
      const form = new FormData();
      form.append('file', file);
      const { data } = await api.post('/wps/import', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      importMsg.value = `Import complete — ${data.added} added, ${data.updated} updated, ${data.skipped} skipped.`;
      await load();
    } catch {
      importMsg.value = 'Import failed. Check the CSV format (WpsNo,Rev,Description,IsActive).';
    } finally {
      importing.value = false;
      e.target.value = '';
    }
  }

  onMounted(load);
</script>
