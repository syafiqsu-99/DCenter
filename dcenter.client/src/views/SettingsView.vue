<template>
  <v-card flat>
    <v-tabs v-model="sub" color="primary" class="mb-4">
      <v-tab value="welders" prepend-icon="mdi-account-hard-hat">Welders</v-tab>
      <v-tab value="dropdowns" prepend-icon="mdi-format-list-bulleted">Dropdown Lists</v-tab>
      <v-tab value="wps" prepend-icon="mdi-clipboard-text-outline">WPS No.</v-tab>
    </v-tabs>

    <v-window v-model="sub">
      <v-window-item value="welders"><WelderTable /></v-window-item>
      <v-window-item value="dropdowns"><LookupTable /></v-window-item>
      <v-window-item value="wps"><WpsTable /></v-window-item>
    </v-window>
  </v-card>
</template>

<script setup>
  import { ref } from 'vue';
  import WelderTable from '@/components/WelderTable.vue';
  import LookupTable from '@/components/LookupTable.vue';
  import WpsTable from '@/components/WpsTable.vue';

  const sub = ref('welders');
</script>
