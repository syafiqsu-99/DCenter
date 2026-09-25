<template>
  <v-card border flat>
    <v-card-text class="d-flex flex-wrap align-center ga-3">
      <v-icon color="primary">mdi-file-delimited-outline</v-icon>
      <div class="flex-grow-1">
        <div class="text-subtitle-1 font-weight-bold">CSV import &amp; export</div>
        <div class="text-caption text-medium-emphasis">
          Rows are matched on Specification + Diameter: existing consumables are updated, new ones are created. Nothing is deleted.
        </div>
      </div>
      <v-btn variant="text" prepend-icon="mdi-file-outline" :loading="busy === 'template'" @click="download(true)">Template</v-btn>
      <v-btn variant="tonal" prepend-icon="mdi-download" :loading="busy === 'export'" @click="download(false)">Export all</v-btn>
      <v-btn color="primary" variant="flat" prepend-icon="mdi-upload" :loading="busy === 'preview'" @click="fileInput?.click()">
        Import CSV
      </v-btn>
      <input ref="fileInput" type="file" accept=".csv,text/csv" hidden @change="onFile">
    </v-card-text>
    <div class="px-4 pb-4">
      <CsvColumnGuide :rows="guide" file-name="Consumables guide.csv" />
    </div>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mx-4 mb-3" closable @click:close="error = ''">
      {{ error }}
    </v-alert>
  </v-card>

  <v-dialog v-model="previewOpen" max-width="1000" scrollable persistent>
    <v-card v-if="result" prepend-icon="mdi-file-check-outline" :title="result.committed ? 'Import complete' : `Check ${fileName}`">
      <v-divider />
      <v-card-text>
        <v-alert v-for="m in result.fileErrors" :key="m" type="error" variant="tonal" density="compact" class="mb-3">{{ m }}</v-alert>
        <div class="d-flex flex-wrap ga-2 mb-3">
          <v-chip color="success" variant="tonal" label>{{ result.created }} new</v-chip>
          <v-chip color="primary" variant="tonal" label>{{ result.updated }} updated</v-chip>
          <v-chip variant="tonal" label>{{ result.unchanged }} unchanged</v-chip>
          <v-chip :color="result.rejected ? 'error' : undefined" variant="tonal" label>{{ result.rejected }} rejected</v-chip>
          <v-spacer />
          <v-switch v-model="problemsOnly" label="Show problems only" density="compact" hide-details inset color="error" />
        </div>
        <v-alert v-if="result.committed" type="success" variant="tonal" density="compact" class="mb-3">
          {{ result.created + result.updated }} consumable(s) saved.
          <template v-if="result.rejected"> {{ result.rejected }} invalid row(s) were skipped — fix them and import again.</template>
        </v-alert>
        <v-data-table-virtual :headers="headers" :items="visibleRows" item-value="line" class="consumable-table" density="compact"
                              height="420" no-data-text="No rows to show.">
          <template #item.action="{ item }">
            <v-chip size="small" :color="ACTION_COLORS[item.action]" variant="tonal" label>{{ item.action }}</v-chip>
          </template>
          <template #item.messages="{ item }">
            <div v-for="m in item.messages" :key="m" class="text-caption"
                 :class="messageClass(item, m)">{{ m }}</div>
          </template>
        </v-data-table-virtual>
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-4 py-3">
        <template v-if="!result.committed">
          <span class="text-body-2 text-medium-emphasis">{{ summary }}</span>
          <v-spacer />
          <v-btn variant="text" :disabled="busy === 'commit'" @click="close">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="busy === 'commit'" :disabled="!pending || result.fileErrors.length > 0"
                 @click="commit">
            {{ result.rejected ? `Import ${pending} valid, skip ${result.rejected}` : `Import ${pending}` }}
          </v-btn>
        </template>
        <template v-else>
          <v-spacer />
          <v-btn color="primary" variant="flat" @click="close">Done</v-btn>
        </template>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { computed, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, errorText, saveBlob, todayIso } from '@/utils/consumables'
  import { itemGuide } from '@/utils/csvGuides'
  import CsvColumnGuide from '@/components/consumables/shared/CsvColumnGuide.vue'

  const ACTION_COLORS = { Create: 'success', Update: 'primary', Unchanged: undefined, Error: 'error' }

  const emit = defineEmits(['imported'])

  const store = useConsumableStore()
  const fileInput = ref(null)
  const file = ref(null)
  const fileName = ref('')
  const result = ref(null)
  const previewOpen = ref(false)
  const problemsOnly = ref(false)
  const busy = ref('')
  const error = ref('')

  const guide = computed(() => itemGuide(store.catalog))

  const headers = [
    { title: 'Line', key: 'line', width: '7%' },
    { title: 'Result', key: 'action', width: '12%' },
    { title: COLUMN.type, key: 'category', width: '17%' },
    { title: COLUMN.diaSpec, key: 'diaSpec', width: '20%' },
    { title: 'Details', key: 'messages', sortable: false, width: '44%' },
  ]

  const visibleRows = computed(() =>
    (result.value?.rows ?? []).filter((r) => !problemsOnly.value || r.action === 'Error' || r.messages.some((m) => m.startsWith('Warning:'))))
  const pending = computed(() => (result.value ? result.value.created + result.value.updated : 0))
  const summary = computed(() => {
    if (!result.value) return ''
    if (!pending.value) return 'Nothing to import — every valid row already matches.'
    return result.value.rejected
      ? 'Invalid rows are not imported. You can fix the file and upload again, or import only the valid rows.'
      : 'Review the changes, then import.'
  })

  function messageClass(row, m) {
    if (m.startsWith('Standardized:')) return 'text-info'
    if (m.startsWith('Warning:')) return 'text-warning'
    return row.action === 'Error' ? 'text-error' : ''
  }

  async function download(template) {
    busy.value = template ? 'template' : 'export'
    error.value = ''
    try {
      const blob = await store.exportItems(template)
      saveBlob(blob, template ? 'Consumables template.csv' : `Consumables ${todayIso()}.csv`)
    } catch (e) {
      error.value = errorText(e, 'Could not download the file.')
    } finally {
      busy.value = ''
    }
  }

  async function onFile(event) {
    const chosen = event.target.files?.[0]
    event.target.value = ''
    if (!chosen) return
    file.value = chosen
    fileName.value = chosen.name
    busy.value = 'preview'
    error.value = ''
    try {
      result.value = await store.importItems(chosen)
      problemsOnly.value = result.value.rejected > 0
      previewOpen.value = true
    } catch (e) {
      error.value = errorText(e, 'Could not read the file.')
    } finally {
      busy.value = ''
    }
  }

  async function commit() {
    busy.value = 'commit'
    try {
      result.value = await store.importItems(file.value, { commit: true, skipInvalid: result.value.rejected > 0 })
      if (result.value.committed) emit('imported', result.value)
    } catch (e) {
      error.value = errorText(e, 'Import failed. Nothing was saved.')
      previewOpen.value = false
    } finally {
      busy.value = ''
    }
  }

  function close() {
    previewOpen.value = false
    result.value = null
    file.value = null
  }
</script>
