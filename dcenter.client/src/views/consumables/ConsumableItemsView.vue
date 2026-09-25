<template>
  <div class="consumable-page">
    <ConsumableCsvPanel @imported="version++" />
    <ConsumableMasterTable :key="version" />
  </div>
</template>

<script setup>
  import { ref } from 'vue'
  import ConsumableCsvPanel from '@/components/consumables/items/ConsumableCsvPanel.vue'
  import ConsumableMasterTable from '@/components/consumables/items/ConsumableMasterTable.vue'

  const version = ref(0)
</script>
