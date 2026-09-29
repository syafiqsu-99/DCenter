<template>
  <v-card flat class="settings-panel-card">
    <v-card-title class="d-flex align-center flex-wrap ga-2">
      Dropdown Lists
      <v-spacer />
      <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search"
                    variant="outlined" density="compact" hide-details clearable
                    style="max-width:220px;" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openNew">Add value</v-btn>
      <v-btn variant="text" size="small" prepend-icon="mdi-download" @click="exportCsv">Export CSV</v-btn>
      <v-btn variant="text" size="small" prepend-icon="mdi-upload" :loading="importing"
             @click="fileInput?.click()">Import CSV</v-btn>
      <input ref="fileInput" type="file" accept=".csv" hidden @change="importCsv" />
    </v-card-title>

    <v-alert v-if="importMsg" type="info" variant="tonal" density="compact" class="mx-4 mt-2"
             closable @click:close="importMsg = ''">{{ importMsg }}</v-alert>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mx-4 mt-2"
             closable @click:close="error = ''">{{ error }}</v-alert>

    <v-card-text class="pb-0 flex-grow-0">
      <v-btn-toggle v-model="category" mandatory divided color="primary" class="mb-2">
        <v-btn v-for="c in categories" :key="c" :value="c">{{ c }}</v-btn>
      </v-btn-toggle>
      <div class="text-caption text-medium-emphasis">
        Drag rows by the handle to reorder.
        <span v-if="search">Clear the search box first — reordering needs the full list.</span>
      </div>
    </v-card-text>

    <div class="card-table-area">
      <v-data-table-virtual :headers="headers" :items="filteredItems" :loading="loading"
                            height="100%" density="comfortable" fixed-header hover
                            item-value="id" :row-props="rowProps"
                            no-data-text="No values match your search."
                            @click:row="(e, { item }) => selectedId = (selectedId === item.id ? null : item.id)">
        <template #loading>
          <v-skeleton-loader type="table-row@6" />
        </template>

        <template #item.drag>
          <v-icon icon="mdi-drag" :class="{ 'text-disabled': !!search }"
                  :style="{ cursor: search ? 'default' : 'grab' }" />
        </template>

        <template #item.isActive="{ item }">
          <v-icon :color="item.isActive ? 'success' : 'grey'">
            {{ item.isActive ? 'mdi-check-circle' : 'mdi-circle-outline' }}
          </v-icon>
        </template>

        <template #item.actions="{ item }">
          <div v-if="selectedId === item.id" class="d-flex justify-end ga-1">
            <v-btn icon="mdi-pencil" variant="text" size="small"
                   :aria-label="`Edit ${item.value}`" @click.stop="openEdit(item)" />
            <v-btn icon="mdi-delete" variant="text" size="small" color="error"
                   :aria-label="`Delete ${item.value}`" @click.stop="askDelete(item)" />
          </div>
        </template>
      </v-data-table-virtual>
    </div>

    <ConfirmDeleteDialog v-model="confirmDelete" title="Delete this value?"
                         :item-label="pendingDelete?.value ?? ''"
                         :loading="deleting" @confirm="doDelete" />

    <v-dialog v-model="dialog" max-width="480">
      <v-card :title="editing.id ? 'Edit value' : 'Add value'"
              :prepend-icon="editing.id ? 'mdi-playlist-edit' : 'mdi-playlist-plus'">
        <v-divider />
        <v-card-text>
          <v-row dense>
            <v-col cols="12" sm="5">
              <v-select v-model="editing.category" :items="categories" label="Category"
                        variant="outlined" density="comfortable" hide-details="auto" />
            </v-col>
            <v-col cols="12" sm="7">
              <v-text-field v-model="editing.value" label="Value"
                            variant="outlined" density="comfortable" hide-details="auto" autofocus />
            </v-col>
            <v-col cols="12">
              <v-switch v-model="editing.isActive" label="Active" color="primary" inset
                        density="comfortable" hide-details />
            </v-col>
          </v-row>
          <v-alert v-if="dialogError" type="error" variant="tonal" density="compact" class="mt-3">{{ dialogError }}</v-alert>
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
  import { computed, onMounted, ref, watch } from 'vue';
  import { useCrudApi } from '@/composables/useCrudApi';
  import { saveBlob } from '@/utils/files';
  import { errorText } from '@/utils/errors';
  import ConfirmDeleteDialog from '@/components/common/ConfirmDeleteDialog.vue';

  const categories = ['Process', 'Size', 'Type', 'Manuf'];
  const category = ref('Process');
  const items = ref([]);
  const search = ref('');
  const loading = ref(false);
  const saving = ref(false);
  const dialog = ref(false);
  const editing = ref(blank());
  const dragId = ref(null);
  const selectedId = ref(null);
  const confirmDelete = ref(false);
  const pendingDelete = ref(null);
  const deleting = ref(false);

  const importing = ref(false);
  const importMsg = ref('');
  const fileInput = ref(null);

  const dialogError = ref('');
  const error = ref('');

  const headers = [
    { title: '',       key: 'drag',     width: '10%', sortable: false },
    { title: 'Value',  key: 'value',    width: '70%' },
    { title: 'Active', key: 'isActive', width: '10%' },
    { title: '',       key: 'actions',  width: '10%', sortable: false, align: 'end' },
  ];

  const filteredItems = computed(() => {
    const q = search.value?.trim().toLowerCase();
    if (!q) return items.value;
    return items.value.filter((i) => i.value.toLowerCase().includes(q));
  });

  const rowProps = ({ item }) => ({
    class: selectedId.value === item.id ? 'bg-blue-grey-lighten-5' : '',
    draggable: !search.value,
    onDragstart: () => { dragId.value = item.id; },
    onDragover: (e) => e.preventDefault(),
    onDrop: () => onDrop(item),
  });

  const lookups = useCrudApi('/lookups');

  function blank() {
    return { id: 0, category: category.value, value: '', isActive: true };
  }

  async function load() {
    loading.value = true;
    try {
      items.value = await lookups.list({ category: category.value });
    } catch (e) {
      error.value = errorText(e, 'Could not load the list. Reload the page to try again.');
    } finally {
      loading.value = false;
    }
  }

  function openNew() {
    editing.value = blank()
    dialogError.value = '';
    dialog.value = true;
  }

  function openEdit(row) {
    editing.value = { ...row }
    dialogError.value = ''
    dialog.value = true;
  }

  async function save() {
    saving.value = true
    dialogError.value = ''
    try {
      const sortOrder = editing.value.id ? editing.value.sortOrder : items.value.length
      const payload = { category: editing.value.category, value: editing.value.value, sortOrder, isActive: editing.value.isActive }
      if (editing.value.id) await lookups.update(editing.value.id, payload)
      else await lookups.create(payload)
      dialog.value = false
      await load()
    } catch (e) {
      dialogError.value = errorText(e, 'Could not save this value.')
    } finally {
      saving.value = false
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
      await lookups.remove(pendingDelete.value.id);
      await load();
    } catch (e) {
      error.value = errorText(e, 'Could not delete this value. Reload and try again.');
    } finally {
      deleting.value = false;
      confirmDelete.value = false;
      pendingDelete.value = null;
    }
  }

  async function onDrop(targetItem) {
    if (search.value || dragId.value === null || dragId.value === targetItem.id) return;
    const from = items.value.findIndex((i) => i.id === dragId.value);
    const to = items.value.findIndex((i) => i.id === targetItem.id);
    dragId.value = null;
    if (from === -1 || to === -1) return;
    const list = [...items.value];
    const [moved] = list.splice(from, 1);
    list.splice(to, 0, moved);
    items.value = list;
    try {
      await lookups.put('reorder', list.map((i) => i.id));
    } catch (e) {
      error.value = errorText(e, 'Could not save the new order. The list was reloaded.');
      await load();
    }
  }

  async function exportCsv() {
    try {
      saveBlob(await lookups.exportBlob(), 'dropdown-lists.csv');
    } catch (e) {
      error.value = errorText(e, 'Could not export the dropdown lists.');
    }
  }

  async function importCsv(e) {
    const file = e.target.files?.[0];
    if (!file) return;
    importing.value = true;
    importMsg.value = '';
    try {
      const data = await lookups.importCsv(file);
      importMsg.value = `Imported: ${data.added} added, ${data.updated} updated, ${data.unchanged} unchanged, ${data.skipped} skipped.`;
      await load();
    } catch (err) {
      importMsg.value = '';
      error.value = errorText(err, 'Import failed. Export the lists to see the expected columns.');
    } finally {
      importing.value = false;
      e.target.value = '';
    }
  }

  watch(category, load);
  onMounted(load);
</script>
