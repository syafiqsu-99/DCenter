<template>
  <v-card flat border>
    <v-card-item>
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-magnify-scan" class="me-2" />
        Traceability
      </v-card-title>
      <v-card-subtitle>Find every joint that used a welder, WPS, part heat number or electrode heat/lot.</v-card-subtitle>
    </v-card-item>
    <v-card-text>
      <v-row dense align="center">
        <v-col cols="12" sm="4" md="3">
          <v-select v-model="traceField" :items="fields" item-title="label" item-value="value"
                    label="Search by" variant="outlined" hide-details
                    @update:model-value="store.searchTrace()" />
        </v-col>
        <v-col cols="12" sm="8" md="6">
          <v-text-field :model-value="traceQuery" :label="current.label" :placeholder="current.hint"
                        variant="outlined" hide-details clearable autofocus
                        prepend-inner-icon="mdi-magnify" :loading="loadingTrace"
                        @update:model-value="onInput" @keydown.enter="store.searchTrace()" />
        </v-col>
      </v-row>
      <v-alert v-if="traceError" type="error" variant="tonal" density="compact" class="mt-3">{{ traceError }}</v-alert>
    </v-card-text>
  </v-card>
</template>

<script setup>
  import { computed, onBeforeUnmount } from 'vue'
  import { storeToRefs } from 'pinia'
  import { useReportInsightsStore } from '@/store/reportInsightsStore'

  const store = useReportInsightsStore()
  const { traceField, traceQuery, loadingTrace, traceError } = storeToRefs(store)

  const fields = [
    { value: 'welder', label: 'Welder', hint: 'Welder no. or name' },
    { value: 'wps', label: 'WPS No.', hint: 'e.g. WPS-101' },
    { value: 'heat', label: 'Part heat number', hint: 'Joining-of or with side' },
    { value: 'heatLot', label: 'Electrode heat/lot', hint: 'Any electrode column' },
  ]
  const current = computed(() => fields.find((f) => f.value === traceField.value) ?? fields[0])

  let timer = null
  function onInput(v) {
    traceQuery.value = v ?? ''
    clearTimeout(timer)
    timer = setTimeout(() => store.searchTrace(), 400)
  }

  onBeforeUnmount(() => clearTimeout(timer))
</script>
