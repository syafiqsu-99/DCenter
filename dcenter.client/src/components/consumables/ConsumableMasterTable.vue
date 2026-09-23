<template>
  <v-card flat class="settings-panel-card">
    <v-card-title class="d-flex align-center flex-wrap ga-2">
      Welding Consumables
      <v-spacer />
      <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search"
                    variant="outlined" density="compact" hide-details clearable style="max-width:220px;" />
      <v-switch v-model="showInactive" label="Show inactive" density="compact" hide-details inset color="primary" />
      <v-btn variant="text" size="small" prepend-icon="mdi-file-delimited-outline" @click="downloadTemplate">
        Import template
      </v-btn>
      <v-btn variant="text" size="small" prepend-icon="mdi-upload" :loading="importing" @click="fileInput?.click()">
        Import opening stock
      </v-btn>
      <input ref="fileInput" type="file" accept=".csv" hidden @change="importCsv" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openNew">Add consumable</v-btn>
    </v-card-title>

    <v-alert v-if="importResult" :type="importResult.errors.length ? 'warning' : 'success'" variant="tonal"
             density="compact" class="mx-4 mb-2" closable @click:close="importResult = null">
      Import complete — {{ importResult.added }} row(s) added, {{ importResult.skipped }} skipped (already imported).
      <ul v-if="importResult.errors.length" class="mt-1 ms-4">
        <li v-for="e in importResult.errors.slice(0, 15)" :key="e.line">Line {{ e.line }}: {{ e.message }}</li>
        <li v-if="importResult.errors.length > 15">…and {{ importResult.errors.length - 15 }} more.</li>
      </ul>
    </v-alert>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mx-4 mb-2" closable
             @click:close="error = ''">{{ error }}</v-alert>

    <div class="card-table-area">
      <v-data-table-virtual :headers="headers" :items="visibleItems" :loading="loading" height="100%"
                            density="comfortable" fixed-header hover item-value="id" class="consumable-table" :row-props="rowProps"
                            no-data-text="No consumables match your search."
                            @click:row="(e, { item }) => selectedId = (selectedId === item.id ? null : item.id)">
        <template #loading>
          <v-skeleton-loader type="table-row@6" />
        </template>
        <template #item.minStockKg="{ item }">{{ item.minStockKg > 0 ? kg(item.minStockKg) : '—' }}</template>
        <template #item.balanceKg="{ item }">
          <span :class="{ 'text-warning font-weight-bold': isLow(item) }">{{ kg(item.balanceKg) }}</span>
        </template>
        <template #item.isActive="{ item }">
          <v-icon :color="item.isActive ? 'success' : 'grey'">
            {{ item.isActive ? 'mdi-check-circle' : 'mdi-circle-outline' }}
          </v-icon>
        </template>
        <template #item.actions="{ item }">
          <div v-if="selectedId === item.id" class="d-flex justify-end">
            <v-tooltip location="top" text="Edit consumable">
              <template #activator="{ props: p }">
                <v-btn v-bind="p" icon="mdi-pencil" variant="text" size="small" :aria-label="`Edit ${item.diaSpec}`"
                       @click.stop="openEdit(item)" />
              </template>
            </v-tooltip>
          </div>
        </template>
      </v-data-table-virtual>
    </div>

    <v-dialog v-model="dialog" max-width="720" persistent>
      <v-card :title="editing.id ? 'Edit consumable' : 'Add consumable'"
              :prepend-icon="editing.id ? 'mdi-pencil' : 'mdi-plus'">
        <v-divider />
        <v-card-text>
          <ConsumableFields v-model="editing" />
          <v-switch v-model="editing.isActive" label="Active (shown in Stock In search)" color="primary"
                    density="comfortable" hide-details inset class="mt-2" />
          <v-alert v-if="dialogError" type="error" variant="tonal" density="compact" class="mt-3">{{ dialogError }}</v-alert>
        </v-card-text>
        <v-divider />
        <v-card-actions class="px-4 py-3">
          <v-spacer />
          <v-btn variant="text" :disabled="saving" @click="dialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="saving" :disabled="!canSave" @click="save">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, downloadCsv, errorText, kg } from '@/utils/consumables'
  import ConsumableFields from '@/components/consumables/ConsumableFields.vue'

  const store = useConsumableStore()
  const items = ref([])
  const search = ref('')
  const showInactive = ref(false)
  const loading = ref(false)
  const saving = ref(false)
  const importing = ref(false)
  const importResult = ref(null)
  const error = ref('')
  const dialog = ref(false)
  const dialogError = ref('')
  const editing = ref(blank())
  const selectedId = ref(null)
  const fileInput = ref(null)

  const headers = [
    { title: COLUMN.type, key: 'consumableType', width: '14%' },
    { title: COLUMN.brand, key: 'manufacturer', width: '15%' },
    { title: COLUMN.diameter, key: 'diameter', width: '11%' },
    { title: COLUMN.spec, key: 'specification', width: '15%' },
    { title: COLUMN.diaSpec, key: 'diaSpec', width: '17%' },
    { title: COLUMN.minStock, key: 'minStockKg', width: '9%', align: 'end' },
    { title: COLUMN.balance, key: 'balanceKg', width: '9%', align: 'end' },
    { title: 'Active', key: 'isActive', width: '5%' },
    { title: '', key: 'actions', width: '5%', sortable: false, align: 'end' },
  ]

  const visibleItems = computed(() => {
    const terms = (search.value ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean)
    return items.value.filter((c) =>
      (showInactive.value || c.isActive) &&
      terms.every((t) => [c.consumableType, c.manufacturer, c.specification, c.diameter, c.diaSpec]
        .some((v) => (v ?? '').toLowerCase().includes(t))))
  })

  const canSave = computed(() => {
    const e = editing.value
    const text = (v) => (v ?? '').toString().trim()
    return !!e.consumableType && !!text(e.manufacturer) && !!text(e.specification) && !!text(e.diameter)
      && Number(e.minStockKg || 0) >= 0 && !saving.value
  })

  const rowProps = ({ item }) => ({
    class: selectedId.value === item.id ? 'bg-blue-grey-lighten-5' : '',
  })

  function blank() {
    return { id: 0, consumableType: null, manufacturer: '', specification: '', diameter: '', minStockKg: 10, isActive: true }
  }

  function isLow(c) {
    return c.minStockKg > 0 && c.balanceKg <= c.minStockKg
  }

  async function load() {
    loading.value = true
    error.value = ''
    try {
      await store.loadCatalog()
      items.value = await store.searchConsumables('', { activeOnly: false, take: 2000 })
    } catch (e) {
      error.value = errorText(e, 'Could not load consumables.')
    } finally {
      loading.value = false
    }
  }

  function openNew() {
    editing.value = blank()
    dialogError.value = ''
    dialog.value = true
  }

  function openEdit(item) {
    editing.value = { ...item }
    dialogError.value = ''
    dialog.value = true
  }

  async function save() {
    saving.value = true
    dialogError.value = ''
    try {
      await store.saveConsumable(editing.value)
      dialog.value = false
      await load()
    } catch (e) {
      dialogError.value = errorText(e, 'Could not save the consumable.')
    } finally {
      saving.value = false
    }
  }

  function downloadTemplate() {
    downloadCsv('Consumable opening stock template.csv', [
      [COLUMN.requestor, COLUMN.date, COLUMN.source, COLUMN.type, COLUMN.brand, COLUMN.diameter, COLUMN.spec,
        COLUMN.lot, COLUMN.receiveQty, COLUMN.balance, COLUMN.minStock],
      ['AIZAT', '15/5/2026', 'Weldshop', 'Electrode Filler', 'ZHONGZHOU', '80/325', 'NiCr-A/Ni202F', '23-10-2032', '20.0', '20.0', '10.0'],
    ])
  }

  async function importCsv(e) {
    const file = e.target.files?.[0]
    if (!file) return
    importing.value = true
    importResult.value = null
    error.value = ''
    try {
      importResult.value = await store.importOpening(file)
      await load()
    } catch (err) {
      error.value = errorText(err, 'Import failed. Check the CSV columns against the template.')
    } finally {
      importing.value = false
      e.target.value = ''
    }
  }

  onMounted(load)
</script>
