<template>
  <v-card border flat class="d-flex flex-column">
    <v-card-title class="d-flex align-center flex-wrap ga-2">
      <v-icon color="blue-grey" class="me-1">mdi-package-variant-closed</v-icon>
      Electrodes in Normal storage
      <span class="text-caption text-medium-emphasis">Tick the electrodes to bake, or send one at a time</span>
      <v-spacer />
      <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Find electrode" clearable variant="outlined"
                    density="compact" hide-details style="max-width:260px; min-width:200px;" />
      <v-btn color="purple" variant="flat" prepend-icon="mdi-fire" :disabled="!selectedRows.length" @click="openSelected">
        Send selected ({{ selectedRows.length }})
      </v-btn>
      <v-btn variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
    </v-card-title>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mx-4 mb-3">{{ error }}</v-alert>
    <v-divider />
    <div ref="tableArea" class="normal-stock-area">
      <v-data-table-virtual v-model="selected" :headers="headers" :items="shown" :loading="loading" item-value="itemId"
                            :item-selectable="(r) => r.normalKg > 0" show-select class="consumable-table" density="comfortable"
                            fixed-header :height="tableHeight"
                            :no-data-text="items.length ? 'No electrode matches the search.' : 'There are no electrodes in Normal storage.'">
        <template #loading><v-skeleton-loader type="table-row@6" /></template>
        <template #item.diaSpec="{ item }">
          <strong>{{ item.diaSpec }}</strong>
          <div class="text-caption text-medium-emphasis">{{ item.holdingOvenType ?? 'Holding oven not set' }} · {{ item.lotCount }} lot(s)</div>
        </template>
        <template #item.normalKg="{ item }">
          <v-chip label size="small" variant="tonal" :color="item.normalKg > 0 ? 'blue-grey' : undefined">{{ kg(item.normalKg) }} kg</v-chip>
        </template>
        <template #item.bakingKg="{ item }">
          <span v-if="item.bakingKg > 0" class="text-deep-orange"><v-icon size="small">mdi-fire</v-icon> {{ kg(item.bakingKg) }}</span>
          <span v-else class="text-disabled">—</span>
        </template>
        <template #item.qty="{ item }">
          <v-text-field v-model.number="qty[item.itemId]" type="number" min="0.01" step="0.01" :max="item.normalKg" suffix="kg"
                        variant="outlined" density="compact" hide-details :disabled="item.normalKg <= 0"
                        :error="!validQty(item)" :aria-label="`Quantity of ${item.diaSpec} to bake`" />
        </template>
        <template #item.actions="{ item }">
          <v-btn color="purple" variant="tonal" prepend-icon="mdi-fire" :disabled="item.normalKg <= 0 || !validQty(item)"
                 @click="openBake(item)">
            Send to baking
          </v-btn>
        </template>
      </v-data-table-virtual>
    </div>
  </v-card>

  <BulkSendToBakeDialog v-model="bulkOpen" :items="bulkRows" @saved="onSent" />
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { computed, onMounted, ref } from 'vue'
  import { useFillHeight } from '@/composables/useFillHeight'
  import { useConsumableStore } from '@/store/consumableStore'
  import { ELECTRODE, errorText, kg } from '@/utils/consumables'
  import BulkSendToBakeDialog from '@/components/consumables/baking/BulkSendToBakeDialog.vue'

  const emit = defineEmits(['sent'])

  const store = useConsumableStore()
  const tableArea = ref(null)
  const tableHeight = useFillHeight(tableArea, 280)
  const items = ref([])
  const selected = ref([])
  const qty = ref({})
  const loading = ref(false)
  const error = ref('')
  const search = ref('')
  const bulkOpen = ref(false)
  const bulkRows = ref([])

  const headers = [
    { title: 'Electrode', key: 'diaSpec', width: '34%' },
    { title: 'In Normal', key: 'normalKg', align: 'end', width: '14%' },
    { title: 'Baking now (kg)', key: 'bakingKg', align: 'end', width: '14%' },
    { title: 'Qty to bake', key: 'qty', sortable: false, width: '18%' },
    { title: '', key: 'actions', sortable: false, align: 'end', width: '20%' },
  ]

  const shown = computed(() => {
    const terms = (search.value ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean)
    return items.value.filter((r) => terms.every((t) => r.diaSpec.toLowerCase().includes(t)))
  })

  const validQty = (r) => r.normalKg <= 0 || (Number(qty.value[r.itemId]) > 0 && Number(qty.value[r.itemId]) <= r.normalKg)

  const selectedRows = computed(() =>
    items.value
      .filter((r) => selected.value.includes(r.itemId) && r.normalKg > 0 && validQty(r))
      .map((r) => ({ ...r, qty: Number(qty.value[r.itemId]) })))

  async function load() {
    loading.value = true
    error.value = ''
    try {
      items.value = await store.loadNormalStock(ELECTRODE)
      qty.value = Object.fromEntries(items.value.map((r) => [r.itemId, r.normalKg]))
      const ids = new Set(items.value.filter((r) => r.normalKg > 0).map((r) => r.itemId))
      selected.value = selected.value.filter((id) => ids.has(id))
    } catch (e) {
      error.value = errorText(e, 'Could not load Normal storage.')
    } finally {
      loading.value = false
    }
  }

  function openBake(r) {
    bulkRows.value = [{ ...r, qty: Number(qty.value[r.itemId]) }]
    bulkOpen.value = true
  }

  function openSelected() {
    bulkRows.value = selectedRows.value
    bulkOpen.value = true
  }

  async function onSent(result) {
    selected.value = []
    emit('sent', result)
    await load()
  }

  defineExpose({ load })
  onMounted(load)
</script>

<style scoped>
  .normal-stock-area {
    height: 420px;
    min-height: 0;
    overflow: hidden;
  }
</style>
