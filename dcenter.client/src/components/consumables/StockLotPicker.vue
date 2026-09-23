<template>
  <v-autocomplete ref="field" :model-value="modelValue" :items="items" :loading="loading"
                  item-title="title" item-value="lotId" clearable auto-select-first
                  :custom-filter="filter" :label="`Stock at ${location} — search specification, brand or lot`"
                  prepend-inner-icon="mdi-magnify" variant="outlined" density="comfortable" hide-details="auto"
                  no-data-text="No stock with a balance at this location."
                  @update:model-value="emit('update:modelValue', $event ?? null)">
    <template #item="{ props: p, item }">
      <v-list-item v-bind="p" :subtitle="`${kg(item.raw.balanceKg)} kg available · received ${fmtDate(item.raw.firstReceivedOn)}`">
        <template #append>
          <v-chip v-if="item.raw.isFifo" size="x-small" color="primary" variant="tonal">Use first</v-chip>
        </template>
      </v-list-item>
    </template>
  </v-autocomplete>
</template>

<script setup>
  import { computed, ref } from 'vue'
  import { fmtDate, kg } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Number, default: null },
    stock: { type: Array, required: true },
    location: { type: String, required: true },
    loading: { type: Boolean, default: false },
  })
  const emit = defineEmits(['update:modelValue'])
  const field = ref(null)

  const items = computed(() => {
    const seen = new Set()
    return props.stock.map((s) => {
      const isFifo = !seen.has(s.consumableId)
      seen.add(s.consumableId)
      return { ...s, isFifo, title: `${s.diaSpec} · ${s.manufacturer} · Lot ${s.lotNumber}` }
    })
  })

  function filter(_value, query, item) {
    const r = item?.raw
    if (!r) return false
    const haystack = `${r.diaSpec} ${r.manufacturer} ${r.lotNumber} ${r.consumableType}`.toLowerCase()
    return (query ?? '').toLowerCase().split(/\s+/).filter(Boolean).every((t) => haystack.includes(t))
  }

  defineExpose({ focus: () => field.value?.focus() })
</script>
