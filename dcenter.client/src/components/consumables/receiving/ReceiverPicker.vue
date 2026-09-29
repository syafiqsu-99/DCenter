<template>
  <v-combobox :model-value="modelValue" :items="items" :loading="loading" :label="COLUMN.receivedBy"
              item-title="welderName" item-value="id" return-object no-filter clearable
              prepend-inner-icon="mdi-account-check-outline" variant="outlined" density="comfortable" hide-details="auto"
              hint="Pick your name. Not listed? Type it and save, or use + to register."
              :no-data-text="loading ? 'Searching…' : 'Not registered yet. Type the full name and save to register it.'"
              @update:search="onSearch" @update:model-value="emit('update:modelValue', $event)">
    <template #item="{ props: p, item }">
      <v-list-item v-bind="p" :title="item.raw.welderName" :subtitle="item.raw.welderNo" />
    </template>
    <template #append>
      <v-btn icon="mdi-account-plus-outline" variant="text" aria-label="Register a new name" title="Register a new name"
             @click="emit('register')" />
    </template>
  </v-combobox>
</template>

<script setup>
  import { onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, debounce } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: [Object, String], default: null },
  })
  const emit = defineEmits(['update:modelValue', 'register'])

  const store = useConsumableStore()
  const items = ref([])
  const loading = ref(false)
  let token = 0

  const load = debounce(async (q) => {
    const current = ++token
    loading.value = true
    try {
      const result = await store.searchWelders(q, false)
      if (current === token) items.value = result
    } catch {
      if (current === token) items.value = []
    } finally {
      if (current === token) loading.value = false
    }
  }, 250)

  function onSearch(q) {
    if (props.modelValue && typeof props.modelValue === 'object' && q === props.modelValue.welderName) return
    load(q ?? '')
  }

  onMounted(() => load(''))
</script>
