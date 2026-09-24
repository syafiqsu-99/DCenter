<template>
  <v-autocomplete :model-value="modelValue" :items="items" :loading="loading" label="Welder"
                  item-title="welderName" item-value="id" return-object no-filter clearable
                  prepend-inner-icon="mdi-account-hard-hat" variant="outlined" density="comfortable" hide-details="auto"
                  :no-data-text="loading ? 'Searching…' : stockOnly ? 'No stock welders found. Set the scope in Settings → Welders.' : 'No welders found.'"
                  @update:search="onSearch" @update:model-value="emit('update:modelValue', $event)">
    <template #item="{ props: p, item }">
      <v-list-item v-bind="p" :title="item.raw.welderName" :subtitle="item.raw.welderNo" />
    </template>
  </v-autocomplete>
</template>

<script setup>
  import { onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { debounce } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Object, default: null },
    stockOnly: { type: Boolean, default: true },
  })
  const emit = defineEmits(['update:modelValue'])

  const store = useConsumableStore()
  const items = ref([])
  const loading = ref(false)
  let token = 0

  const load = debounce(async (q) => {
    const current = ++token
    loading.value = true
    try {
      const result = await store.searchWelders(q, props.stockOnly)
      if (current === token) items.value = result
    } catch {
      if (current === token) items.value = []
    } finally {
      if (current === token) loading.value = false
    }
  }, 250)

  function onSearch(q) {
    if (props.modelValue && q === props.modelValue.welderName) return
    load(q ?? '')
  }

  onMounted(() => load(''))
</script>
