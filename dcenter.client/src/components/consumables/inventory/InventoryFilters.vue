<template>
  <v-card border flat>
    <v-card-text class="d-flex flex-wrap align-center ga-3">
      <v-text-field v-model="filters.search" prepend-inner-icon="mdi-magnify" label="Search" clearable v-bind="field"
                    style="max-width:260px;" />
      <v-select v-model="filters.category" :items="[ALL, ...store.catalog.categories]" :label="COLUMN.type" v-bind="field"
                style="max-width:220px;" />
      <v-btn-toggle v-model="filters.view" mandatory divided density="compact" variant="outlined" color="primary">
        <v-btn value="items" size="small">By consumable</v-btn>
        <v-btn value="lots" size="small">By lot (sheet)</v-btn>
      </v-btn-toggle>
      <v-switch v-model="filters.lowOnly" label="Low stock only" color="warning" density="compact" hide-details inset />
      <v-switch v-if="filters.view === 'items'" v-model="filters.refillOnly" label="Needs refill" color="warning"
                density="compact" hide-details inset />
      <v-switch v-else v-model="filters.includeZero" label="Include empty lots" color="primary" density="compact"
                hide-details inset @update:model-value="reload" />
      <v-chip v-if="filtered" color="warning" variant="tonal" closable prepend-icon="mdi-filter-remove-outline"
              @click="clearFilters" @click:close="clearFilters">Clear filters</v-chip>
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="reload">Refresh</v-btn>
      <v-btn variant="text" prepend-icon="mdi-file-delimited-outline" @click="exportCsv">Export CSV</v-btn>
    </v-card-text>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mx-4 mb-3">{{ error }}</v-alert>
  </v-card>
</template>

<script setup>
  import { computed, onMounted, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { ALL, COLUMN, downloadCsv, errorText, fmtDate, todayIso } from '@/utils/consumables'

  const store = useConsumableStore()
  const filters = store.inventoryFilters
  const field = { variant: 'outlined', density: 'compact', hideDetails: true }
  const error = ref('')

  const filtered = computed(() =>
    !!(filters.search ?? '').trim() || filters.category !== ALL || filters.lowOnly || filters.refillOnly)

  function clearFilters() {
    Object.assign(filters, { search: '', category: ALL, lowOnly: false, refillOnly: false })
  }

  const loading = computed(() => (filters.view === 'items' ? store.loadingBalances : store.loadingLotStock))

  async function load(force) {
    error.value = ''
    try {
      if (filters.view === 'items') await store.loadBalances(force)
      else await store.loadLotStock(force)
    } catch (e) {
      error.value = errorText(e, 'Could not load the inventory.')
    }
  }

  function reload() {
    return load(true)
  }

  function exportCsv() {
    const n = (v) => Number(v ?? 0).toFixed(2)
    if (filters.view === 'items') {
      const header = [COLUMN.diaSpec, COLUMN.type, COLUMN.normal, COLUMN.baking, COLUMN.activated, COLUMN.total, COLUMN.minStock,
        COLUMN.activatedMin, 'Lots', 'Last pickup', 'Low stock', 'Needs refill']
      const rows = store.filteredBalances.map((r) => [r.diaSpec, r.category, n(r.normalKg), n(r.bakingKg), n(r.activatedKg), n(r.totalKg),
        n(r.minStockKg), n(r.activatedMinKg), r.lotCount, fmtDate(r.lastIssuedOn), r.isLow ? 'Yes' : '', r.needsRefill ? 'Yes' : ''])
      downloadCsv(`Consumable balances ${todayIso()}.csv`, [header, ...rows])
      return
    }
    const header = [COLUMN.receivedBy, COLUMN.date, COLUMN.source, COLUMN.type, COLUMN.brand, COLUMN.diameter, COLUMN.spec,
      COLUMN.lot, COLUMN.diaSpec, COLUMN.receiveQty, COLUMN.take, COLUMN.normal, COLUMN.baking, COLUMN.activated, COLUMN.balance]
    const rows = store.filteredLotStock.map((r) => [r.receivedBy ?? '', fmtDate(r.date), r.source ?? '', r.category, r.brand,
      r.diameter, r.specification, r.lotNumber, r.diaSpec, n(r.receiveQtyKg), n(r.takeKg), n(r.normalKg), n(r.bakingKg), n(r.activatedKg),
      n(r.balanceKg)])
    downloadCsv(`Consumable lots ${todayIso()}.csv`, [header, ...rows])
  }

  watch(() => filters.view, () => load(false))
  onMounted(() => load(true))
</script>
