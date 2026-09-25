<template>
  <div class="consumable-page">
    <v-btn-toggle v-model="view" mandatory divided density="compact" variant="outlined" color="primary" class="align-self-start flex-shrink-0">
      <v-btn value="receive" prepend-icon="mdi-tray-arrow-down">Receive</v-btn>
      <v-btn value="import" prepend-icon="mdi-file-upload-outline">Import stock (CSV)</v-btn>
    </v-btn-toggle>
    <div v-if="view === 'receive'" class="receive-grid fill-card">
      <ReceiveForm />
      <TodayReceiptsList />
    </div>
    <StockImportPanel v-else />
  </div>
</template>

<script setup>
  import { useSubView } from '@/composables/useSubView'
  import ReceiveForm from '@/components/consumables/receiving/ReceiveForm.vue'
  import TodayReceiptsList from '@/components/consumables/receiving/TodayReceiptsList.vue'
  import StockImportPanel from '@/components/consumables/receiving/StockImportPanel.vue'

  const view = useSubView(['receive', 'import'])
</script>

<style scoped>
  .receive-grid {
    display: grid;
    gap: 12px;
    grid-template-columns: minmax(0, 2fr) minmax(0, 1fr);
    grid-template-rows: minmax(0, 1fr);
  }

  @media (max-width: 1279px) {
    .receive-grid {
      grid-template-columns: minmax(0, 1fr);
      grid-template-rows: auto minmax(320px, 1fr);
      flex: none;
      overflow: visible;
    }
  }
</style>
