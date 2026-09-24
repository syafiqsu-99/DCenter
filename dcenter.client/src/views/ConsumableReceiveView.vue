<template>
  <v-row>
    <v-col cols="12" lg="8"><ReceiveForm /></v-col>
    <v-col cols="12" lg="4"><TodayReceiptsList /></v-col>
  </v-row>
</template>

<script setup>
  import ReceiveForm from '@/components/consumables/ReceiveForm.vue'
  import TodayReceiptsList from '@/components/consumables/TodayReceiptsList.vue'
</script>
