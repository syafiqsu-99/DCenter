<script setup>
import { storeToRefs } from 'pinia';
import { useReportStore } from '@/store/reportStore';

const store = useReportStore();
const { searchInput, loading } = storeToRefs(store);
</script>

<template>
  <v-card class="mb-4" flat border>
    <v-card-text>
      <v-row align="center" dense>
        <v-col cols="12" sm="8" md="6">
          <v-text-field
            v-model="searchInput"
            label="Job number"
            placeholder="e.g. 47762415"
            prepend-inner-icon="mdi-magnify"
            hide-details
            clearable
            @keyup.enter="store.search()"
          />
        </v-col>
        <v-col cols="12" sm="4" md="2">
          <v-btn
            color="primary"
            block
            :loading="loading"
            :disabled="!searchInput"
            @click="store.search()"
          >
            Search
          </v-btn>
        </v-col>
      </v-row>
    </v-card-text>
  </v-card>
</template>
