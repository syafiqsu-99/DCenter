<template>
  <v-row dense>
    <v-col cols="12" sm="6">
      <v-select v-model="model.category" :items="store.catalog.categories" :label="COLUMN.type" v-bind="field"
                :disabled="lockIdentity" />
    </v-col>
    <v-col cols="12" sm="6">
      <v-combobox v-model="model.specification" :items="specs" :label="COLUMN.spec" v-bind="field" :disabled="lockIdentity"
                  :hint="isNew(specs, model.specification) ? newHint : ''" persistent-hint />
    </v-col>
    <v-col cols="12" sm="4">
      <v-combobox v-model="model.diameter" :items="sizes" :label="COLUMN.diameter" v-bind="field" :disabled="lockIdentity"
                  hint="Numbers only, saved as e.g. 2.40 or 3.20" persistent-hint
                  @blur="model.diameter = formatDiameter(model.diameter)" />
    </v-col>
    <v-col cols="12" sm="4">
      <v-text-field v-model.number="model.minStockKg" type="number" min="0" step="0.5" :label="COLUMN.minStock"
                    suffix="kg" v-bind="field" hint="Total stock that triggers reorder" persistent-hint />
    </v-col>
    <v-col cols="12" sm="4">
      <v-text-field v-model.number="model.activatedMinKg" type="number" min="0" step="0.5" :label="COLUMN.activatedMin"
                    suffix="kg" v-bind="field" hint="Rack level that prompts a transfer" persistent-hint />
    </v-col>
    <v-col cols="12" sm="4">
      <v-text-field v-model="model.finishThresholdKg" type="number" min="0" step="0.1" :label="COLUMN.finishThreshold"
                    suffix="kg" v-bind="field" clearable
                    :placeholder="`Default ${kg(store.catalog.finishThresholdKg)}`" persistent-placeholder />
    </v-col>
    <v-col v-if="model.category === ELECTRODE" cols="12" sm="4">
      <v-select v-model="model.holdingOvenType" :items="store.catalog.ovenTypes" label="Holding oven type" v-bind="field"
                clearable hint="Which holding oven these electrodes go into" persistent-hint />
    </v-col>
    <v-col v-if="diaSpec" cols="12" sm="8" class="d-flex align-center">
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
  import { COLUMN, ELECTRODE, formatDiameter, kg } from '@/utils/consumables'

  const model = defineModel({ type: Object, required: true })
  defineProps({ lockIdentity: { type: Boolean, default: false } })

  const store = useConsumableStore()
  const lookupStore = useLookupStore()
  const { options } = storeToRefs(lookupStore)
  lookupStore.load()

  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const newHint = 'New value — it will be added to the dropdown lists on save'

  const sizes = computed(() => options.value?.Size ?? [])
  const specs = computed(() => options.value?.Type ?? [])

  const text = (v) => (v ?? '').toString().trim()
  const isNew = (list, value) => !!text(value) && !list.some((x) => text(x).toLowerCase() === text(value).toLowerCase())
  const diaSpec = computed(() => `${text(model.value.diameter)} ${text(model.value.specification).toUpperCase()}`.trim())
</script>
