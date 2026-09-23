<template>
  <v-row>
    <v-col cols="12" lg="8"><ReceiveForm /></v-col>
    <v-col cols="12" lg="4"><TodayTransactionsList type="Receive" title="Today's stock in" /></v-col>
  </v-row>
</template>

<script setup>
  import ReceiveForm from '@/components/consumables/ReceiveForm.vue'
  import TodayTransactionsList from '@/components/consumables/TodayTransactionsList.vue'
</script>
