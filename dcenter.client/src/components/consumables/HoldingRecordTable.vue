<template>
  <v-card border flat>
    <v-card-text class="d-flex flex-wrap align-center ga-3">
      <v-text-field v-model="filters.from" type="date" label="From" v-bind="field" style="max-width:170px;" />
      <v-text-field v-model="filters.to" type="date" label="To" v-bind="field" style="max-width:170px;" />
      <v-text-field v-model="filters.q" prepend-inner-icon="mdi-magnify" label="Holding no., baking no., lot, welder…" clearable
                    v-bind="field" class="flex-grow-1" style="min-width:220px;" />
      <v-btn variant="text" prepend-icon="mdi-printer-outline" :disabled="!items.length" @click="printRecords">Print</v-btn>
      <v-btn variant="text" prepend-icon="mdi-file-delimited-outline" :disabled="!items.length" @click="exportCsv">Export</v-btn>
    </v-card-text>
    <v-divider />
    <v-data-table-virtual :headers="headers" :items="items" :loading="loading" item-value="id" class="consumable-table"
                          density="compact" fixed-header height="calc(100vh - 400px)" no-data-text="No holding records.">
      <template #loading><v-skeleton-loader type="table-row@8" /></template>
      <template #item.holdingNo="{ item }">
        <span :class="{ 'text-decoration-line-through text-disabled': item.isVoided }">{{ item.holdingNo }}</span>
      </template>
      <template #item.holdingDate="{ item }">{{ fmtDate(item.holdingDate) }}</template>
      <template #item.diaSpec="{ item }">
        {{ item.diaSpec }}
        <div class="text-caption text-medium-emphasis">{{ item.brand }} · Lot {{ item.lotNumber }}</div>
      </template>
      <template #item.target="{ item }">
        <v-chip v-if="item.isFinishedAfterBaking" size="small" color="info" variant="tonal" label>Finished After Baking</v-chip>
        <v-chip v-else size="small" color="indigo" variant="tonal" label>{{ item.compartmentLabel }}</v-chip>
      </template>
      <template #item.quantityKg="{ item }">{{ kg(item.quantityKg) }}</template>
      <template #item.welderName="{ item }">{{ item.welderName || '—' }}</template>
      <template #item.created="{ item }">
        <span class="text-caption">{{ fmtDateTime(item.createdAt) }}<br>{{ item.createdBy }}</span>
      </template>
    </v-data-table-virtual>
    <div v-if="items.length < total" class="d-flex justify-center py-2">
      <v-btn variant="tonal" :loading="loadingMore" @click="loadMore">Load more ({{ total - items.length }} left)</v-btn>
    </div>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-3">{{ error }}</v-alert>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { onMounted, reactive, ref, watch } from 'vue'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, debounce, downloadCsv, errorText, fmtDate, fmtDateTime, kg, monthStartIso, openPrint, todayIso } from '@/utils/consumables'

  const PAGE_SIZE = 100

  const store = useConsumableStore()
  const router = useRouter()
  const field = { variant: 'outlined', density: 'compact', hideDetails: true }
  const filters = reactive({ from: monthStartIso(), to: todayIso(), q: '' })
  const items = ref([])
  const total = ref(0)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref('')
  let token = 0

  const headers = [
    { title: 'Holding No', key: 'holdingNo', width: '10%' },
    { title: COLUMN.date, key: 'holdingDate', width: '8%' },
    { title: 'Baking No', key: 'bakingNo', width: '10%' },
    { title: 'Consumable', key: 'diaSpec', width: '20%' },
    { title: 'Compartment', key: 'target', sortable: false, width: '14%' },
    { title: 'KG', key: 'quantityKg', align: 'end', width: '7%' },
    { title: 'Welder Name', key: 'welderName', width: '13%' },
    { title: 'Remarks', key: 'remarks', width: '8%' },
    { title: 'Entered', key: 'created', sortable: false, width: '10%' },
  ]

  function params(skip) {
    return { from: filters.from || undefined, to: filters.to || undefined, q: (filters.q ?? '').trim() || undefined, skip, take: PAGE_SIZE }
  }

  async function reload() {
    const current = ++token
    loading.value = true
    error.value = ''
    try {
      const page = await store.loadHoldingRecords(params(0))
      if (current !== token) return
      items.value = page.items
      total.value = page.total
    } catch (e) {
      if (current === token) error.value = errorText(e, 'Could not load holding records.')
    } finally {
      if (current === token) loading.value = false
    }
  }

  async function loadMore() {
    loadingMore.value = true
    try {
      const page = await store.loadHoldingRecords(params(items.value.length))
      items.value = [...items.value, ...page.items]
      total.value = page.total
    } catch (e) {
      error.value = errorText(e, 'Could not load more records.')
    } finally {
      loadingMore.value = false
    }
  }

  function printRecords() {
    openPrint(router, 'holding-records', { from: filters.from, to: filters.to, q: (filters.q ?? '').trim() })
  }

  function exportCsv() {
    const header = ['Holding No', COLUMN.date, 'Welder Name', 'Baking No', COLUMN.diaSpec, COLUMN.brand, COLUMN.lot,
      'Compartment', 'Finished After Baking', 'Quantity (KG)', 'Remarks', 'Voided', 'Entered by']
    const rows = items.value.map((h) => [h.holdingNo, fmtDate(h.holdingDate), h.welderName ?? '', h.bakingNo, h.diaSpec, h.brand,
      h.lotNumber, h.compartmentLabel ?? '', h.isFinishedAfterBaking ? 'Yes' : '', Number(h.quantityKg).toFixed(2),
      h.remarks ?? '', h.isVoided ? 'Yes' : '', h.createdBy ?? ''])
    downloadCsv(`Holding records ${todayIso()}.csv`, [header, ...rows])
  }

  watch(() => ({ ...filters }), debounce(reload, 300), { deep: true })
  onMounted(reload)
</script>
