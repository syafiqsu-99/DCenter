<template>
  <v-autocomplete ref="field" :model-value="modelValue" :items="items" :loading="loading"
                  item-title="label" item-value="id" return-object no-filter clearable auto-select-first
                  label="Consumable — search brand, specification or diameter" placeholder="e.g. E7018 3.2"
                  prepend-inner-icon="mdi-magnify" variant="outlined" density="comfortable" hide-details="auto"
                  @update:search="onSearch" @update:focused="onFocus"
                  @update:model-value="emit('update:modelValue', $event ?? null)">
    <template #item="{ props: p, item }">
      <v-list-item v-bind="p" :title="item.raw.label"
                   :subtitle="`${item.raw.consumableType} · Balance ${kg(item.raw.balanceKg)} kg`" />
    </template>
    <template #no-data>
      <v-list-item title="No matching consumable." subtitle="Use “New consumable…” below." />
    </template>
    <template #append-item>
      <v-divider class="my-1" />
      <v-list-item prepend-icon="mdi-plus" title="New consumable…" base-color="primary" @click="emit('new')" />
    </template>
  </v-autocomplete>
</template>

<script setup>
  import { ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { debounce, kg } from '@/utils/consumables'

  defineProps({ modelValue: { type: Object, default: null } })
  const emit = defineEmits(['update:modelValue', 'new'])

  const store = useConsumableStore()
  const field = ref(null)
  const items = ref([])
  const loading = ref(false)
  let token = 0

  const search = debounce(async (q) => {
    const current = ++token
    loading.value = true
    try {
      const data = await store.searchConsumables(q)
      if (current === token) items.value = data.map((c) => ({ ...c, label: `${c.diaSpec} · ${c.manufacturer}` }))
    } catch {
      if (current === token) items.value = []
    } finally {
      if (current === token) loading.value = false
    }
  })

  function onSearch(q) {
    search((q ?? '').trim())
  }

  function onFocus(focused) {
    if (focused && !items.value.length) search('')
  }

  defineExpose({ focus: () => field.value?.focus() })
</script>
