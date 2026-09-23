<template>
  <v-card border flat class="pa-3">
    <v-row dense align="center">
      <v-col cols="6" sm="3" md="2">
        <v-text-field v-model="filters.from" type="date" label="From" v-bind="field" />
      </v-col>
      <v-col cols="6" sm="3" md="2">
        <v-text-field v-model="filters.to" type="date" label="To" v-bind="field" />
      </v-col>
      <v-col cols="12" sm="6" md="2">
        <v-select v-model="filters.type" :items="types" label="Type" v-bind="field" clearable />
      </v-col>
      <v-col cols="12" md="4">
        <v-text-field v-model="filters.q" prepend-inner-icon="mdi-magnify"
                      label="Search requestor, lot, specification, brand, remarks" v-bind="field" clearable />
      </v-col>
      <v-col cols="12" md="auto">
        <LocationToggle v-model="filters.location" allow-all />
      </v-col>
    </v-row>
  </v-card>
</template>

<script setup>
  import { useConsumableStore } from '@/store/consumableStore'
  import LocationToggle from '@/components/consumables/LocationToggle.vue'

  const store = useConsumableStore()
  const filters = store.historyFilters
  const field = { variant: 'outlined', density: 'compact', hideDetails: true }
  const types = [
    { title: 'Stock In', value: 'Receive' },
    { title: 'Stock Out', value: 'Issue' },
    { title: 'Void', value: 'Void' },
  ]
</script>
