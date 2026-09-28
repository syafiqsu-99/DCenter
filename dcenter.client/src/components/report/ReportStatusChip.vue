<template>
  <v-chip v-if="status" :color="status === 'Completed' ? 'success' : 'warning'" size="small" variant="tonal">
    {{ status }}
  </v-chip>
  <span v-else class="text-medium-emphasis">—</span>
</template>

<script setup>
  defineProps({ status: { type: String, default: '' } });
</script>
