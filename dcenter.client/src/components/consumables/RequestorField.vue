<template>
  <v-combobox :model-value="modelValue" :items="items" :label="label" :loading="loading"
              item-title="welderName" item-value="welderName" :return-object="false"
              no-filter clearable variant="outlined" density="comfortable" hide-details="auto"
              prepend-inner-icon="mdi-account-hard-hat-outline"
              :rules="required ? [(v) => !!(v ?? '').toString().trim() || `${label} is required`] : []"
              @update:search="onSearch" @update:focused="onFocus" @update:model-value="onPick">
    <template #item="{ props: p, item }">
      <v-list-item v-bind="p" :subtitle="item.raw.welderNo" />
    </template>
  </v-combobox>
</template>

<script setup>
  import { onMounted, ref } from 'vue'
  import api from '@/utils/api'
  import { debounce } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: String, default: '' },
    label: { type: String, default: 'Requestor' },
    required: { type: Boolean, default: false },
  })
  const emit = defineEmits(['update:modelValue'])

  const items = ref([])
  const loading = ref(false)
  let token = 0
  let lastQuery = null

  async function fetchWelders(q) {
    if (q === lastQuery && items.value.length) return
    const current = ++token
    loading.value = true
    try {
      const { data } = await api.get('/welders/search', { params: { q } })
      if (current !== token) return
      items.value = data
      lastQuery = q
    } catch {
      if (current === token) items.value = []
    } finally {
      if (current === token) loading.value = false
    }
  }

  const search = debounce(fetchWelders)

  function onSearch(q) {
    const term = (q ?? '').trim()
    search(term === (props.modelValue ?? '').trim() ? '' : term)
  }

  function onFocus(focused) {
    if (focused) fetchWelders('')
  }

  function onPick(value) {
    emit('update:modelValue', typeof value === 'string' ? value : (value?.welderName ?? ''))
  }

  onMounted(() => fetchWelders(''))
</script>
