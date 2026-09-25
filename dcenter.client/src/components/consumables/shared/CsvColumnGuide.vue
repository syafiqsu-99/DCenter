<template>
  <v-expansion-panels v-model="open" variant="accordion" flat>
    <v-expansion-panel value="guide" class="border">
      <v-expansion-panel-title class="py-2">
        <v-icon color="info" size="small" class="me-2">mdi-help-circle-outline</v-icon>
        What goes in each column
        <span class="text-caption text-medium-emphasis ms-2">Values are matched ignoring case; close misspellings get a suggestion</span>
      </v-expansion-panel-title>
      <v-expansion-panel-text>
        <div class="d-flex justify-end mb-2">
          <v-btn size="small" variant="text" prepend-icon="mdi-download" @click="download">Download guide</v-btn>
        </div>
        <v-table density="compact" class="guide-table">
          <thead>
            <tr>
              <th style="width:16%;">Column</th>
              <th style="width:12%;">Required</th>
              <th style="width:30%;">Format</th>
              <th style="width:30%;">Allowed values</th>
              <th style="width:12%;">Example</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="g in rows" :key="g.column">
              <td class="font-weight-bold">{{ g.column }}</td>
              <td><v-chip size="x-small" :color="g.required === 'No' ? undefined : 'primary'" variant="tonal" label>{{ g.required }}</v-chip></td>
              <td class="text-caption">{{ g.format }}</td>
              <td class="text-caption">{{ g.allowed || '—' }}</td>
              <td class="text-caption"><code v-if="g.example">{{ g.example }}</code></td>
            </tr>
          </tbody>
        </v-table>
      </v-expansion-panel-text>
    </v-expansion-panel>
  </v-expansion-panels>
</template>

<script setup>
  import { downloadCsv } from '@/utils/consumables'

  const props = defineProps({
    rows: { type: Array, required: true },
    fileName: { type: String, required: true },
  })

  const open = defineModel('open', { type: String, default: null })

  function download() {
    downloadCsv(props.fileName, [
      ['Column', 'Required', 'Format', 'Allowed values', 'Example'],
      ...props.rows.map((g) => [g.column, g.required, g.format, g.allowed, g.example]),
    ])
  }
</script>

<style scoped>
  .guide-table td {
    vertical-align: top;
    padding-top: 6px !important;
    padding-bottom: 6px !important;
  }
</style>
