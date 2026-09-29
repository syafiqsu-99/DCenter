<template>
  <div>
    <v-tabs v-if="!route.meta.reportEditor" :model-value="route.name" color="primary"
            density="comfortable" show-arrows class="mb-4">
      <v-tab v-for="t in tabs" :key="t.name" :value="t.name" :to="{ name: t.name }" :prepend-icon="t.icon">
        {{ t.label }}
      </v-tab>
    </v-tabs>

    <v-alert v-if="error" type="error" variant="tonal" class="mb-4" closable
             @click:close="store.error = ''">{{ error }}</v-alert>

    <router-view />
  </div>
</template>

<script setup>
  import { storeToRefs } from 'pinia'
  import { useRoute } from 'vue-router'
  import { useReportStore } from '@/store/reportStore'

  const route = useRoute()
  const store = useReportStore()
  const { error } = storeToRefs(store)

  const tabs = [
    { name: 'report-list', label: 'Reports', icon: 'mdi-file-document-multiple-outline' },
    { name: 'report-dashboard', label: 'Dashboard', icon: 'mdi-view-dashboard-outline' },
    { name: 'report-trace', label: 'Traceability', icon: 'mdi-magnify-scan' },
  ]
</script>
