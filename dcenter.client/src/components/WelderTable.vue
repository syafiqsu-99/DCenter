<template>
  <v-card flat>
    <v-card-title class="d-flex align-center">
      Welders
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openNew">Add welder</v-btn>
    </v-card-title>

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

    <v-dialog v-model="dialog" max-width="420">
      <v-card>
        <v-card-title>{{ editing.id ? 'Edit welder' : 'Add welder' }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="editing.welderName" label="Welder Name" />
          <v-text-field v-model="editing.welderNo" label="Welder No." />
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
  import { onMounted, ref } from 'vue';
  import api from '@/utils/api';

  const items = ref([]);
  const loading = ref(false);
  const dialog = ref(false);
  const editing = ref(null);

  const headers = [
      { title: 'Welder Name', key: 'welderName' },
      { title: 'Welder No.', key: 'welderNo' },
      { title: 'Active', key: 'isActive' },
      { title: '', key: 'actions', sortable: false, align: 'end' },
  ];

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
      if (editing.value.id) {
        await api.put(`/welders/${editing.value.id}`, editing.value);
      } else {
        await api.post('/welders', editing.value);
      }
      dialog.value = false;
      await load();
  }

  async function remove(row) {
      await api.delete(`/welders/${row.id}`);
      await load();
  }

  onMounted(load);
</script>
