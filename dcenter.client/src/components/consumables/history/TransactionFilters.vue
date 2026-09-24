<template>
  <v-card border flat>
    <v-card-text class="d-flex flex-wrap align-center ga-3">
      <v-text-field v-model="filters.from" type="date" label="From" v-bind="field" style="max-width:170px;" />
      <v-text-field v-model="filters.to" type="date" label="To" v-bind="field" style="max-width:170px;" />
      <v-select v-model="filters.type" :items="typeItems" item-title="title" item-value="value" label="Type" clearable
                v-bind="field" style="max-width:170px;" />
      <v-select v-model="filters.stage" :items="store.catalog.stages" label="Stage" clearable v-bind="field"
                style="max-width:150px;" />
      <v-select v-model="filters.category" :items="[ALL, ...store.catalog.categories]" :label="COLUMN.type" v-bind="field"
                style="max-width:210px;" />
      <div style="min-width:220px;"><WelderPicker v-model="filters.welder" :stock-only="false" density="compact" /></div>
      <v-text-field v-model="filters.q" prepend-inner-icon="mdi-magnify" label="Txn no., lot, brand, welder, remarks…"
                    clearable v-bind="field" style="min-width:240px;" class="flex-grow-1" />
      <v-chip v-if="filters.compartmentId" closable color="indigo" variant="tonal"
              @click:close="filters.compartmentId = null; filters.compartmentCode = ''">
        Compartment {{ filters.compartmentCode }}
      </v-chip>
    </v-card-text>
  </v-card>
</template>

<script setup>
  import { useConsumableStore } from '@/store/consumableStore'
  import { ALL, COLUMN, TXN_FILTER_TYPES, TXN_LABELS } from '@/utils/consumables'
  import WelderPicker from '@/components/consumables/shared/WelderPicker.vue'

  const store = useConsumableStore()
  const filters = store.historyFilters
  const field = { variant: 'outlined', density: 'compact', hideDetails: true }
  const typeItems = TXN_FILTER_TYPES.map((t) => ({ title: TXN_LABELS[t], value: t }))
</script>
