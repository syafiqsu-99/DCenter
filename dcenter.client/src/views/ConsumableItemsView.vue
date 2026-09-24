<template>
  <div class="d-flex flex-column ga-4">
    <ConsumableCsvPanel @imported="version++" />
    <div style="height: calc(100vh - 360px); min-height: 420px;">
      <ConsumableMasterTable :key="version" />
    </div>
  </div>
</template>

<script setup>
  import { ref } from 'vue'
  import ConsumableCsvPanel from '@/components/consumables/ConsumableCsvPanel.vue'
  import ConsumableMasterTable from '@/components/consumables/ConsumableMasterTable.vue'

  const version = ref(0)
</script>
