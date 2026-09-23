<template>
  <v-sheet color="grey-lighten-4" rounded class="pa-3">
    <v-row dense>
      <v-col v-for="d in items" :key="d.label" cols="6" sm="4" :md="md">
        <div class="text-caption text-medium-emphasis">{{ d.label }}</div>
        <div class="text-body-2 font-weight-medium" :class="d.class">{{ d.value || '—' }}</div>
      </v-col>
    </v-row>
  </v-sheet>
</template>

<script setup>
  defineProps({
    items: { type: Array, required: true },
    md: { type: [Number, String], default: 4 },
  })
</script>
