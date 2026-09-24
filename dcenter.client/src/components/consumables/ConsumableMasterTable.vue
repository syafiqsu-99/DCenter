<template>
  <v-card border flat class="fill-card">
    <v-card-title class="d-flex align-center flex-wrap ga-2">
      Welding Consumables
      <v-spacer />
      <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search" variant="outlined" density="compact"
                    hide-details clearable style="max-width:220px;" />
      <v-switch v-model="showInactive" label="Show inactive" density="compact" hide-details inset color="primary" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openNew">Add consumable</v-btn>
    </v-card-title>

    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mx-4 mb-2" closable
             @click:close="error = ''">{{ error }}</v-alert>

    <div ref="tableArea" class="fill">
      <v-data-table-virtual :headers="headers" :items="visibleItems" :loading="loading" :height="tableHeight"
                            density="comfortable" fixed-header hover item-value="id" class="consumable-table"
                            :row-props="rowProps" no-data-text="No consumables match your search."
                            @click:row="(e, { item }) => selectedId = (selectedId === item.id ? null : item.id)">
        <template #loading><v-skeleton-loader type="table-row@6" /></template>
        <template #item.holdingOvenType="{ item }">
          <span v-if="item.category !== ELECTRODE" class="text-disabled">—</span>
          <span v-else-if="item.holdingOvenType">{{ item.holdingOvenType }}</span>
          <v-chip v-else size="x-small" color="warning" variant="tonal" label>Not set</v-chip>
        </template>
        <template #item.minStockKg="{ item }">{{ item.minStockKg > 0 ? kg(item.minStockKg) : '—' }}</template>
        <template #item.activatedMinKg="{ item }">{{ item.activatedMinKg > 0 ? kg(item.activatedMinKg) : '—' }}</template>
        <template #item.finishThresholdKg="{ item }">
          {{ item.finishThresholdKg === null ? `Default (${kg(store.catalog.finishThresholdKg)})` : kg(item.finishThresholdKg) }}
        </template>
        <template #item.totalKg="{ item }">
          <span :class="{ 'text-warning font-weight-bold': isLow(item) }">{{ kg(item.totalKg) }}</span>
        </template>
        <template #item.isActive="{ item }">
          <v-icon :color="item.isActive ? 'success' : 'grey'">
            {{ item.isActive ? 'mdi-check-circle' : 'mdi-circle-outline' }}
          </v-icon>
        </template>
        <template #item.actions="{ item }">
          <div v-if="selectedId === item.id" class="d-flex justify-end">
            <v-btn icon="mdi-pencil" variant="text" size="small" :aria-label="`Edit ${item.diaSpec}`"
                   @click.stop="openEdit(item)" />
          </div>
        </template>
      </v-data-table-virtual>
    </div>

    <v-dialog v-model="dialog" max-width="720" persistent>
      <v-card :title="editing.id ? 'Edit consumable' : 'Add consumable'"
              :prepend-icon="editing.id ? 'mdi-pencil' : 'mdi-plus'">
        <v-divider />
        <v-card-text>
          <ItemFields v-model="editing" :lock-identity="editing.id > 0 && editing.totalKg !== 0" />
          <v-switch v-model="editing.isActive" label="Active" color="primary" inset density="comfortable" hide-details class="mt-2" />
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
  import { useFillHeight } from '@/composables/useFillHeight'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, ELECTRODE, errorText, kg } from '@/utils/consumables'
  import ItemFields from '@/components/consumables/ItemFields.vue'

  const tableArea = ref(null)
  const tableHeight = useFillHeight(tableArea)
  const store = useConsumableStore()
  const items = ref([])
  const search = ref('')
  const showInactive = ref(false)
  const loading = ref(false)
  const saving = ref(false)
  const dialog = ref(false)
  const editing = ref(blank())
  const selectedId = ref(null)
  const error = ref('')
  const dialogError = ref('')

  const headers = [
    { title: COLUMN.type, key: 'category', width: '12%' },
    { title: COLUMN.spec, key: 'specification', width: '12%' },
    { title: COLUMN.diameter, key: 'diameter', width: '7%' },
    { title: COLUMN.diaSpec, key: 'diaSpec', width: '12%' },
    { title: 'Holding oven', key: 'holdingOvenType', width: '8%' },
    { title: COLUMN.minStock, key: 'minStockKg', align: 'end', width: '9%' },
    { title: COLUMN.activatedMin, key: 'activatedMinKg', align: 'end', width: '9%' },
    { title: COLUMN.finishThreshold, key: 'finishThresholdKg', align: 'end', width: '11%' },
    { title: 'Stock (KG)', key: 'totalKg', align: 'end', width: '8%' },
    { title: 'Active', key: 'isActive', width: '6%' },
    { title: '', key: 'actions', sortable: false, align: 'end', width: '6%' },
  ]

  const rowProps = ({ item }) => ({ class: selectedId.value === item.id ? 'bg-blue-grey-lighten-5' : '' })

  const visibleItems = computed(() => {
    const terms = (search.value ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean)
    return items.value.filter((i) =>
      (showInactive.value || i.isActive) &&
      terms.every((t) => [i.category, i.specification, i.diameter, i.diaSpec].some((v) => (v ?? '').toLowerCase().includes(t))))
  })

  const canSave = computed(() =>
    !!editing.value.category && !!(editing.value.specification ?? '').trim() && !!(editing.value.diameter ?? '').toString().trim())

  function blank() {
    return {
      id: 0, category: null, specification: '', diameter: '', minStockKg: 0, activatedMinKg: 0,
      finishThresholdKg: null, holdingOvenType: null, isActive: true, totalKg: 0,
    }
  }

  function isLow(item) {
    return item.isActive && item.minStockKg > 0 && item.totalKg <= item.minStockKg
  }

  async function load() {
    loading.value = true
    error.value = ''
    try {
      await store.loadCatalog()
      items.value = await store.searchItems('', { activeOnly: false, take: 2000 })
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

  function openEdit(row) {
    editing.value = { ...row }
    dialogError.value = ''
    dialog.value = true
  }

  async function save() {
    saving.value = true
    dialogError.value = ''
    try {
      await store.saveItem(editing.value)
      dialog.value = false
      await load()
    } catch (e) {
      dialogError.value = errorText(e, 'Could not save the consumable.')
    } finally {
      saving.value = false
    }
  }

  onMounted(load)
</script>
