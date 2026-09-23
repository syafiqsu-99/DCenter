<template>
  <v-chip :color="TXN_COLORS[type] ?? 'grey'" size="x-small" variant="flat" label
          :class="{ 'text-decoration-line-through': voided }">
    {{ label }}
  </v-chip>
</template>

<script setup>
  import { computed } from 'vue'
  import { TXN_COLORS } from '@/utils/consumables'

  const props = defineProps({
    type: { type: String, required: true },
    voided: { type: Boolean, default: false },
  })

  const labels = { Receive: 'Stock In', Issue: 'Stock Out', Void: 'Void' }
  const label = computed(() => labels[props.type] ?? props.type)
</script>
