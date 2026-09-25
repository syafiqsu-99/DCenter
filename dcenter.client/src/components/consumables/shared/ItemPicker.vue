<template>
  <v-autocomplete ref="field" :model-value="modelValue" :items="items" :loading="loading" :label="label"
                  item-title="diaSpec" item-value="id" return-object no-filter clearable :autofocus="autofocus"
                  variant="outlined" density="comfortable" hide-details="auto" :disabled="disabled"
                  :no-data-text="loading ? 'Searching…' : 'No consumables found'"
                  @update:search="onSearch" @update:model-value="emit('update:modelValue', $event)">
    <template #item="{ props: p, item }">
      <v-list-item v-bind="p" :title="item.raw.diaSpec"
                   :subtitle="`${item.raw.category} · Normal ${kg(item.raw.normalKg)} kg · Activated ${kg(item.raw.activatedKg)} kg`" />
    </template>
    <template v-if="$slots['append-item']" #append-item><slot name="append-item" /></template>
  </v-autocomplete>
</template>

<script setup>
  import { onMounted, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { debounce, kg } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Object, default: null },
    label: { type: String, default: 'Consumable (Dia & Spec)' },
    category: { type: String, default: null },
    disabled: { type: Boolean, default: false },
    autofocus: { type: Boolean, default: false },
  })
  const emit = defineEmits(['update:modelValue'])

  const store = useConsumableStore()
  const field = ref(null)
  const items = ref([])
  const loading = ref(false)
  let token = 0

  const load = debounce(async (q) => {
    const current = ++token
    loading.value = true
    try {
      const result = await store.searchItems(q, { category: props.category })
      if (current === token) items.value = result
    } catch {
      if (current === token) items.value = []
    } finally {
      if (current === token) loading.value = false
    }
  }, 250)

  function onSearch(q) {
    if (props.modelValue && q === props.modelValue.diaSpec) return
    load(q ?? '')
  }

  watch(() => props.category, () => load(''))
  onMounted(() => load(''))

  defineExpose({ focus: () => field.value?.focus(), reload: () => load('') })
</script>
