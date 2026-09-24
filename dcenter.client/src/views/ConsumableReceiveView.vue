<template>
  <div class="consumable-page receive-page">
    <ReceiveForm />
    <TodayReceiptsList />
  </div>
</template>

<script setup>
  import ReceiveForm from '@/components/consumables/ReceiveForm.vue'
  import TodayReceiptsList from '@/components/consumables/TodayReceiptsList.vue'
</script>

<style scoped>
  .receive-page {
    display: grid;
    grid-template-columns: minmax(0, 2fr) minmax(0, 1fr);
    grid-template-rows: minmax(0, 1fr);
  }

  @media (max-width: 1279px) {
    .receive-page {
      grid-template-columns: minmax(0, 1fr);
      grid-template-rows: auto minmax(320px, 1fr);
      height: auto;
    }
  }
</style>
