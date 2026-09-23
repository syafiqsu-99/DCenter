<template>
  <div class="d-flex align-center ga-3" :class="{ 'w-100': block }">
    <span v-if="label" class="text-body-2 text-medium-emphasis text-no-wrap">{{ label }}</span>
    <v-btn-toggle :model-value="modelValue" mandatory divided color="primary" variant="outlined"
                  :density="density" :class="{ 'flex-grow-1': block }"
                  @update:model-value="emit('update:modelValue', $event)">
      <v-btn v-if="allowAll" :value="ALL" :class="{ 'flex-grow-1': block }">All</v-btn>
      <v-btn v-for="loc in store.catalog.locations" :key="loc" :value="loc" :class="{ 'flex-grow-1': block }">
        {{ loc }}
      </v-btn>
    </v-btn-toggle>
  </div>
</template>

<script setup>
  import { useConsumableStore } from '@/store/consumableStore'
  import { ALL } from '@/utils/consumables'

  defineProps({
    modelValue: { type: String, default: '' },
    allowAll: { type: Boolean, default: false },
    label: { type: String, default: '' },
    block: { type: Boolean, default: false },
    density: { type: String, default: 'comfortable' },
  })
  const emit = defineEmits(['update:modelValue'])
  const store = useConsumableStore()
</script>
