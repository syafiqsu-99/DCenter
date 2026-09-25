<template>
  <v-chip :color="voided ? 'grey' : (TXN_COLORS[type] ?? 'default')" size="small" variant="tonal" label
          :class="{ 'text-decoration-line-through': voided }">
    {{ TXN_LABELS[type] ?? type }}
  </v-chip>
</template>

<script setup>
  import { TXN_COLORS, TXN_LABELS } from '@/utils/consumables'

  defineProps({
    type: { type: String, required: true },
    voided: { type: Boolean, default: false },
  })
</script>
