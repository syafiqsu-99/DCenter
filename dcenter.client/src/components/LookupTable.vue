<template>
  <v-card flat>
    <v-card-title class="d-flex align-center">
      Dropdown Lists
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openNew">Add value</v-btn>
    </v-card-title>

    <v-card-text>
      <v-btn-toggle v-model="category" mandatory divided color="primary" class="mb-2">
        <v-btn v-for="c in categories" :key="c" :value="c">{{ c }}</v-btn>
      </v-btn-toggle>
    </v-card-text>

    <v-data-table :headers="headers" :items="items" :loading="loading" density="comfortable">
      <template #item.isActive="{ item }">
        <v-icon :color="item.isActive ? 'success' : 'grey'">
          {{ item.isActive ? 'mdi-check-circle' : 'mdi-circle-outline' }}
        </v-icon>
      </template>
      <template #item.actions="{ item }">
        <v-btn icon="mdi-pencil" variant="text" size="small" @click="openEdit(item)" />
        <v-btn icon="mdi-delete" variant="text" size="small" color="error" @click="remove(item)" />
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="420">
      <v-card>
        <v-card-title>{{ editing.id ? 'Edit value' : 'Add value' }} — {{ editing.category }}</v-card-title>
        <v-card-text>
          <v-select v-model="editing.category" :items="categories" label="Category" />
          <v-text-field v-model="editing.value" label="Value" />
          <v-text-field v-model="editing.sortOrder" label="Sort order" type="number" />
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
  import { onMounted, ref, watch } from 'vue';
  import api from '@/utils/api';

  const categories = ['Process', 'Size', 'Type', 'Manuf'];
  const category = ref('Process');
  const items = ref([]);
  const loading = ref(false);
  const dialog = ref(false);
  const editing = ref(null);

  const headers = [
      { title: 'Value', key: 'value' },
      { title: 'Sort', key: 'sortOrder' },
      { title: 'Active', key: 'isActive' },
      { title: '', key: 'actions', sortable: false, align: 'end' },
  ];

  function blank() {
      return { id: 0, category: category.value, value: '', sortOrder: 0, isActive: true };
  }

  async function load() {
      loading.value = true;
      try {
        const { data } = await api.get('/lookups', { params: { category: category.value } });
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
        category: editing.value.category,
        value: editing.value.value,
        sortOrder: Number(editing.value.sortOrder) || 0,
        isActive: editing.value.isActive,
      };
      if (editing.value.id) {
        await api.put(`/lookups/${editing.value.id}`, payload);
      } else {
        await api.post('/lookups', payload);
      }
      dialog.value = false;
      await load();
  }

  async function remove(row) {
      await api.delete(`/lookups/${row.id}`);
      await load();
  }

  watch(category, load);
  onMounted(load);
</script>
