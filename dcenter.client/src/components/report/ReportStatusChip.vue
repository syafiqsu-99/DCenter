<template>
  <v-chip v-if="status" :color="status === REPORT_STATUS.completed ? 'success' : 'warning'" size="small" variant="tonal">
    {{ status }}
  </v-chip>
  <span v-else class="text-medium-emphasis">—</span>
</template>

<script setup>
  import { REPORT_STATUS } from '@/utils/constants'

  defineProps({ status: { type: String, default: '' } })
</script>
