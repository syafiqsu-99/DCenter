<template>
  <v-sheet border rounded class="pa-3 h-100 d-flex flex-column justify-center"
           :color="over ? 'red-lighten-5' : 'blue-grey-lighten-5'">
    <div class="text-caption text-medium-emphasis">AVAILABLE AT SOURCE</div>
    <div class="text-h4 font-weight-bold">{{ available === null ? '—' : kg(available) }}<span class="text-body-1 ms-1">kg</span></div>
    <div class="text-body-2" :class="over ? 'text-error' : 'text-medium-emphasis'">
      <template v-if="available === null">Select a lot to see its balance.</template>
      <template v-else-if="over">Exceeds available by {{ kg(quantity - available) }} kg</template>
      <template v-else-if="quantity > 0">After this stock out: {{ kg(available - quantity) }} kg</template>
      <template v-else>Enter the Take/KG quantity.</template>
    </div>
  </v-sheet>
</template>

<script setup>
  import { computed } from 'vue'
  import { kg } from '@/utils/consumables'

  const props = defineProps({
    available: { type: Number, default: null },
    quantity: { type: Number, default: 0 },
  })
  const over = computed(() => props.available !== null && props.quantity > props.available)
</script>
