<template>
  <v-card flat class="settings-panel-card">
    <v-card-title class="d-flex align-center flex-wrap ga-2">
      Welders
      <v-spacer />
      <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search"
                    variant="outlined" density="compact" hide-details clearable style="max-width:220px;" />
      <v-select v-model="scopeFilter" :items="scopeFilterItems" item-title="title" item-value="value" label="Scope"
                variant="outlined" density="compact" hide-details style="max-width:190px;" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openNew">Add welder</v-btn>
    </v-card-title>

    <div v-if="selected.length" class="d-flex flex-wrap align-center ga-2 px-4 pb-2">
      <span class="text-body-2">{{ selected.length }} selected</span>
      <v-btn size="small" variant="tonal" color="primary" :loading="bulkSaving" @click="setScope('ReportAndStock')">
        Set Report &amp; Stock
      </v-btn>
      <v-btn size="small" variant="tonal" :loading="bulkSaving" @click="setScope('Report')">Set Report only</v-btn>
      <v-btn size="small" variant="text" @click="selected = []">Clear</v-btn>
    </div>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mx-4 mb-2" closable
             @click:close="error = ''">{{ error }}</v-alert>

    <div class="card-table-area">
      <v-data-table-virtual v-model="selected" :headers="headers" :items="filteredItems" :loading="loading"
                            height="100%" density="comfortable" fixed-header hover show-select
                            item-value="id" :row-props="rowProps"
                            no-data-text="No welders match your search."
                            @click:row="(e, { item }) => selectedId = (selectedId === item.id ? null : item.id)">
        <template #loading><v-skeleton-loader type="table-row@6" /></template>
        <template #item.usageScope="{ item }">
          <v-chip size="small" variant="tonal" :color="item.usageScope === 'ReportAndStock' ? 'primary' : undefined" label>
            {{ SCOPE_LABELS[item.usageScope] ?? item.usageScope }}
          </v-chip>
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

    <v-dialog v-model="dialog" max-width="520">
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
              <v-select v-model="editing.usageScope" :items="scopeItems" item-title="title" item-value="value" label="Used for"
                        variant="outlined" density="comfortable" hide-details="auto"
                        hint="Report & Stock welders appear at the consumable Welder Counter" persistent-hint />
            </v-col>
            <v-col cols="12">
              <v-switch v-model="editing.isActive" label="Active" color="primary" inset density="comfortable" hide-details />
            </v-col>
          </v-row>
          <v-alert v-if="dialogError" type="error" variant="tonal" density="compact" class="mt-3">{{ dialogError }}</v-alert>
        </v-card-text>
        <v-divider />
        <v-card-actions class="px-4 py-3">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="saving" :disabled="!editing.welderName?.trim()" @click="save">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-card>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import api from '@/utils/api'
  import { errorText } from '@/utils/consumables'
  import ConfirmDeleteDialog from '@/components/common/ConfirmDeleteDialog.vue'

  const SCOPE_LABELS = { Report: 'Report', ReportAndStock: 'Report & Stock' }
  const scopeItems = [
    { title: 'Weld Report only', value: 'Report' },
    { title: 'Weld Report & consumable stock', value: 'ReportAndStock' },
  ]
  const scopeFilterItems = [{ title: 'All scopes', value: null }, ...scopeItems]

  const items = ref([])
  const search = ref('')
  const scopeFilter = ref(null)
  const loading = ref(false)
  const saving = ref(false)
  const bulkSaving = ref(false)
  const dialog = ref(false)
  const editing = ref(blank())
  const selectedId = ref(null)
  const selected = ref([])
  const confirmDelete = ref(false)
  const pendingDelete = ref(null)
  const deleting = ref(false)
  const error = ref('')
  const dialogError = ref('')

  const headers = [
    { title: 'Welder Name', key: 'welderName', width: '38%' },
    { title: 'Welder No.', key: 'welderNo', width: '20%' },
    { title: 'Scope', key: 'usageScope', width: '18%' },
    { title: 'Active', key: 'isActive', width: '10%' },
    { title: '', key: 'actions', width: '10%', sortable: false, align: 'end' },
  ]

  const rowProps = ({ item }) => ({ class: selectedId.value === item.id ? 'bg-blue-grey-lighten-5' : '' })

  const filteredItems = computed(() => {
    const q = search.value?.trim().toLowerCase()
    return items.value.filter((w) =>
      (!scopeFilter.value || w.usageScope === scopeFilter.value) &&
      (!q || w.welderName.toLowerCase().includes(q) || w.welderNo.toLowerCase().includes(q)))
  })

  function blank() {
    return { id: 0, welderName: '', welderNo: '', isActive: true, usageScope: 'Report' }
  }

  async function load() {
    loading.value = true
    try {
      const { data } = await api.get('/welders')
      items.value = data
    } catch (e) {
      error.value = errorText(e, 'Could not load welders.')
    } finally {
      loading.value = false
    }
  }

  function openNew() {
    editing.value = blank()
    dialogError.value = ''
    dialog.value = true
  }

  function openEdit(row) {
    editing.value = { ...row }
    dialogError.value = ''
    dialog.value = true
  }

  async function save() {
    saving.value = true
    dialogError.value = ''
    try {
      if (editing.value.id) await api.put(`/welders/${editing.value.id}`, editing.value)
      else await api.post('/welders', editing.value)
      dialog.value = false
      await load()
    } catch (e) {
      dialogError.value = errorText(e, 'Could not save the welder.')
    } finally {
      saving.value = false
    }
  }

  async function setScope(usageScope) {
    bulkSaving.value = true
    error.value = ''
    try {
      await api.put('/welders/scope', { ids: selected.value, usageScope })
      selected.value = []
      await load()
    } catch (e) {
      error.value = errorText(e, 'Could not update the scope.')
    } finally {
      bulkSaving.value = false
    }
  }

  function askDelete(row) {
    pendingDelete.value = row
    confirmDelete.value = true
  }

  async function doDelete() {
    if (!pendingDelete.value) return
    deleting.value = true
    try {
      await api.delete(`/welders/${pendingDelete.value.id}`)
      await load()
    } catch (e) {
      error.value = errorText(e, 'Could not delete the welder.')
    } finally {
      deleting.value = false
      confirmDelete.value = false
      pendingDelete.value = null
    }
  }

  onMounted(load)
</script>
