<template>
  <v-card border flat class="pa-3">
    <v-row dense align="center">
      <v-col cols="12" md="4">
        <v-text-field v-model="filters.search" prepend-inner-icon="mdi-magnify"
                      label="Search brand, specification, diameter, lot or requestor" v-bind="field" clearable />
      </v-col>
      <v-col cols="12" sm="4" md="2">
        <v-select v-model="filters.type" :items="store.catalog.types" :label="COLUMN.type" v-bind="field" clearable />
      </v-col>
      <v-col cols="12" sm="4" md="3">
        <v-autocomplete v-model="filters.brand" :items="store.brandOptions" :label="COLUMN.brand" v-bind="field" clearable />
      </v-col>
      <v-col cols="12" sm="4" md="3">
        <v-autocomplete v-model="filters.diameter" :items="store.diameterOptions" :label="COLUMN.diameter" v-bind="field" clearable />
      </v-col>
      <v-col cols="12" md="auto">
        <LocationToggle v-model="filters.location" allow-all />
      </v-col>
      <v-col cols="12" md="auto">
        <v-select v-model="filters.view" :items="views" label="View" v-bind="field" style="min-width:210px;" />
      </v-col>
      <v-col cols="auto">
        <v-switch v-model="filters.includeZero" label="Include zero balance" color="primary" density="compact"
                  hide-details inset @update:model-value="reload" />
      </v-col>
      <v-col cols="auto">
        <v-switch v-model="filters.lowOnly" label="Low stock only" color="warning" density="compact" hide-details inset />
      </v-col>
      <v-spacer />
      <v-col cols="auto" class="d-flex ga-1">
        <v-btn variant="text" prepend-icon="mdi-download" :disabled="!store.filteredCard.length" @click="exportCsv">
          Export CSV
        </v-btn>
        <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="store.loadingStockCard" @click="reload">Refresh</v-btn>
      </v-col>
    </v-row>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-2">{{ error }}</v-alert>
  </v-card>
</template>

<script setup>
  import { onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, downloadCsv, errorText, fmtDate, todayIso } from '@/utils/consumables'
  import LocationToggle from '@/components/consumables/LocationToggle.vue'

  const store = useConsumableStore()
  const filters = store.filters
  const field = { variant: 'outlined', density: 'compact', hideDetails: true }
  const error = ref('')

  const views = [
    { title: 'Stock card (sheet layout)', value: 'card' },
    { title: 'By consumable', value: 'consumable' },
    { title: `By ${COLUMN.brand}`, value: 'brand' },
    { title: `By ${COLUMN.spec}`, value: 'spec' },
    { title: `By ${COLUMN.diameter}`, value: 'diameter' },
    { title: `By ${COLUMN.lot}`, value: 'lot' },
  ]

  async function reload() {
    error.value = ''
    try {
      await store.loadStockCard(true)
    } catch (e) {
      error.value = errorText(e, 'Could not load the inventory.')
    }
  }

  function exportCsv() {
    const header = [COLUMN.requestor, COLUMN.date, COLUMN.source, COLUMN.type, COLUMN.brand, COLUMN.diameter,
      COLUMN.spec, COLUMN.lot, COLUMN.diaSpec, COLUMN.receiveQty, COLUMN.take, COLUMN.balance]
    const rows = store.filteredCard.map((r) => [
      r.requestor ?? '', fmtDate(r.date), r.source, r.consumableType, r.brand, r.diameter, r.specification,
      r.lotNumber, r.diaSpec, r.receiveQtyKg.toFixed(2), r.takes.map((t) => t.quantityKg.toFixed(2)).join('; '),
      r.balanceKg.toFixed(2),
    ])
    downloadCsv(`Consumable stock ${todayIso()}.csv`, [header, ...rows])
  }

  onMounted(async () => {
    error.value = ''
    try {
      await store.loadStockCard()
    } catch (e) {
      error.value = errorText(e, 'Could not load the inventory.')
    }
  })
</script>
