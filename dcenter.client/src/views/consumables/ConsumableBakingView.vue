<template>
  <div class="consumable-page">
    <v-btn-toggle v-model="view" mandatory divided density="compact" variant="outlined" color="primary" class="align-self-start flex-shrink-0">
      <v-btn value="board" prepend-icon="mdi-view-column-outline">Baking board</v-btn>
      <v-btn value="ovens" prepend-icon="mdi-view-grid-outline">Holding ovens</v-btn>
      <v-btn value="records" prepend-icon="mdi-fire">Baking records</v-btn>
      <v-btn value="holdings" prepend-icon="mdi-archive-arrow-down-outline">Holding records</v-btn>
    </v-btn-toggle>
    <BakingBoard v-if="view === 'board'" />
    <OvenBoard v-else-if="view === 'ovens'" />
    <BakingRecordTable v-else-if="view === 'records'" />
    <HoldingRecordTable v-else />
  </div>
</template>

<script setup>
  import { useSubView } from '@/composables/useSubView'
  import BakingBoard from '@/components/consumables/baking/BakingBoard.vue'
  import OvenBoard from '@/components/consumables/holding/OvenBoard.vue'
  import BakingRecordTable from '@/components/consumables/baking/BakingRecordTable.vue'
  import HoldingRecordTable from '@/components/consumables/baking/HoldingRecordTable.vue'

  const view = useSubView(['board', 'ovens', 'records', 'holdings'])
</script>
