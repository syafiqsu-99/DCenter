<template>
  <div class="consumable-page">
    <ConsumableCsvPanel @imported="version++" />
    <ConsumableMasterTable :key="version" />
  </div>
</template>

<script setup>
  import { ref } from 'vue'
  import ConsumableCsvPanel from '@/components/consumables/ConsumableCsvPanel.vue'
  import ConsumableMasterTable from '@/components/consumables/ConsumableMasterTable.vue'

  const version = ref(0)
</script>
