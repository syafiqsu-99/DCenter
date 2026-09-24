<template>
  <div>
    <v-btn-toggle v-model="tab" mandatory divided density="compact" variant="outlined" color="primary" class="mb-3">
      <v-btn value="board" prepend-icon="mdi-view-column-outline">Board</v-btn>
      <v-btn value="records" prepend-icon="mdi-fire">Baking records</v-btn>
      <v-btn value="holdings" prepend-icon="mdi-archive-arrow-down-outline">Holding records</v-btn>
    </v-btn-toggle>
    <BakingBoard v-if="tab === 'board'" />
    <BakingRecordTable v-else-if="tab === 'records'" />
    <HoldingRecordTable v-else />
  </div>
</template>

<script setup>
  import { ref } from 'vue'
  import BakingBoard from '@/components/consumables/BakingBoard.vue'
  import BakingRecordTable from '@/components/consumables/BakingRecordTable.vue'
  import HoldingRecordTable from '@/components/consumables/HoldingRecordTable.vue'

  const tab = ref('board')
</script>
