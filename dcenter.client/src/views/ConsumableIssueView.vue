<template>
  <v-row>
    <v-col cols="12" lg="8"><IssueForm /></v-col>
    <v-col cols="12" lg="4"><TodayTransactionsList type="Issue" title="Today's stock out" /></v-col>
  </v-row>
</template>

<script setup>
  import IssueForm from '@/components/consumables/IssueForm.vue'
  import TodayTransactionsList from '@/components/consumables/TodayTransactionsList.vue'
</script>
