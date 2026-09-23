<template>
  <v-row dense>
    <v-col cols="12" sm="6">
      <v-select v-model="model.consumableType" :items="store.catalog.types" :label="COLUMN.type" v-bind="field" autofocus />
    </v-col>
    <v-col cols="12" sm="6">
      <v-combobox v-model="model.manufacturer" :items="brands" :label="COLUMN.brand" v-bind="field"
                  :hint="isNew(brands, model.manufacturer) ? newHint : ''" persistent-hint />
    </v-col>
    <v-col cols="12" sm="4">
      <v-combobox v-model="model.diameter" :items="sizes" :label="COLUMN.diameter" v-bind="field"
                  :hint="isNew(sizes, model.diameter) ? newHint : ''" persistent-hint />
    </v-col>
    <v-col cols="12" sm="5">
      <v-combobox v-model="model.specification" :items="specs" :label="COLUMN.spec" v-bind="field"
                  :hint="isNew(specs, model.specification) ? newHint : ''" persistent-hint />
    </v-col>
    <v-col cols="12" sm="3">
      <v-text-field v-model.number="model.minStockKg" type="number" min="0" step="0.5"
                    :label="COLUMN.minStock" suffix="kg" v-bind="field" />
    </v-col>
    <v-col v-if="diaSpec" cols="12">
      <span class="text-caption text-medium-emphasis">{{ COLUMN.diaSpec }}:</span>
      <strong class="ms-1">{{ diaSpec }}</strong>
    </v-col>
  </v-row>
</template>

<script setup>
  import { computed } from 'vue'
  import { storeToRefs } from 'pinia'
  import { useConsumableStore } from '@/store/consumableStore'
  import { useLookupStore } from '@/store/lookupStore'
  import { COLUMN } from '@/utils/consumables'

  const model = defineModel({ type: Object, required: true })

  const store = useConsumableStore()
  const lookupStore = useLookupStore()
  const { options } = storeToRefs(lookupStore)
  lookupStore.load(true)

  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const newHint = 'New value — it will be added to the dropdown lists on save'

  const brands = computed(() => options.value?.Manuf ?? [])
  const sizes = computed(() => options.value?.Size ?? [])
  const specs = computed(() => options.value?.Type ?? [])

  const text = (v) => (v ?? '').toString().trim()
  const isNew = (list, value) => !!text(value) && !list.some((x) => text(x).toLowerCase() === text(value).toLowerCase())
  const diaSpec = computed(() => `${text(model.value.diameter)} ${text(model.value.specification)}`.trim())
</script>
