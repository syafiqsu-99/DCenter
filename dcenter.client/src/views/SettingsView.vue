<template>
  <v-card flat border>
    <v-toolbar color="transparent" density="comfortable">
      <v-icon icon="mdi-cog-outline" class="ms-4 me-3" />
      <div>
        <div class="text-subtitle-1 font-weight-medium">Settings</div>
        <div class="text-caption text-medium-emphasis">Manage welders, dropdown lists, and WPS numbers</div>
      </div>

      <v-spacer />

      <v-btn-toggle v-model="sub" color="primary" variant="outlined" divided mandatory class="me-4">
        <v-btn v-for="s in sections" :key="s.value" :value="s.value" :prepend-icon="s.icon">
          {{ s.label }}
        </v-btn>
      </v-btn-toggle>
    </v-toolbar>

    <v-divider />

    <v-window v-model="sub" class="pa-4">
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

  const sections = [
    { value: 'welders', label: 'Welders', icon: 'mdi-account-hard-hat' },
    { value: 'dropdowns', label: 'Dropdown Lists', icon: 'mdi-format-list-bulleted' },
    { value: 'wps', label: 'WPS No.', icon: 'mdi-clipboard-text-outline' },
  ];

  const sub = ref('welders');
</script>
