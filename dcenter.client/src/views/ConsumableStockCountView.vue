<template>
  <div>
    <v-btn-toggle v-model="tab" mandatory divided density="compact" variant="outlined" color="primary" class="mb-3">
      <v-btn value="count" prepend-icon="mdi-clipboard-edit-outline">New count</v-btn>
      <v-btn value="history" prepend-icon="mdi-clipboard-text-clock-outline">Past counts</v-btn>
    </v-btn-toggle>
    <StockCountSheet v-if="tab === 'count'" @posted="tab = 'history'" />
    <StockCountHistory v-else />
  </div>
</template>

<script setup>
  import { ref } from 'vue'
  import StockCountSheet from '@/components/consumables/StockCountSheet.vue'
  import StockCountHistory from '@/components/consumables/StockCountHistory.vue'

  const tab = ref('count')
</script>
