<template>
  <v-card border flat>
    <v-card-text class="d-flex flex-wrap align-center ga-3">
      <v-icon color="primary">mdi-file-upload-outline</v-icon>
      <div class="flex-grow-1">
        <div class="text-subtitle-1 font-weight-bold">Import existing stock from CSV</div>
        <div class="text-caption text-medium-emphasis">
          Each row is received as one lot under reference {{ OPENING }}. Unknown consumables are created automatically.
          All rows share one transaction number, so voiding it in History undoes the whole file.
        </div>
      </div>
      <v-btn variant="text" prepend-icon="mdi-file-outline" :loading="busy === 'template'" @click="downloadTemplate">Template</v-btn>
      <v-btn color="primary" variant="flat" prepend-icon="mdi-upload" :loading="busy === 'preview'" :disabled="busy === 'commit'"
             @click="fileInput?.click()">
        {{ result ? 'Upload another file' : 'Upload CSV' }}
      </v-btn>
      <input ref="fileInput" type="file" accept=".csv,text/csv" hidden @change="onFile">
    </v-card-text>
    <div class="px-4 pb-4">
      <CsvColumnGuide v-model:open="guideOpen" :rows="guide" file-name="Opening stock guide.csv" />
    </div>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mx-4 mb-4" closable @click:close="error = ''">
      {{ error }}
    </v-alert>
  </v-card>

  <v-card v-if="result" border flat class="fill-card preview-card">
    <v-card-text class="d-flex flex-wrap align-center ga-2">
      <span class="text-subtitle-2 me-2">{{ fileName }}</span>
      <v-chip color="success" variant="tonal" label>{{ result.ready }} ready</v-chip>
      <v-chip color="primary" variant="tonal" label>{{ result.newConsumables }} new consumable(s)</v-chip>
      <v-chip :color="result.rejected ? 'error' : undefined" variant="tonal" label>{{ result.rejected }} with errors</v-chip>
      <v-chip v-if="result.skipped" variant="tonal" label>{{ result.skipped }} skipped</v-chip>
      <v-chip variant="outlined" label>{{ kg(totalKg) }} kg</v-chip>
      <v-spacer />
      <v-switch v-model="problemsOnly" label="Show problems only" density="compact" hide-details inset color="error" />
    </v-card-text>
    <v-alert v-for="m in result.fileErrors" :key="m" type="error" variant="tonal" density="compact" class="mx-4 mb-3">{{ m }}</v-alert>
    <v-alert v-if="result.committed" type="success" variant="tonal" density="compact" class="mx-4 mb-3">
      {{ result.ready }} row(s) received as <strong>{{ result.txnNo }}</strong>.
      <template v-if="result.rejected"> {{ result.rejected }} invalid row(s) were not imported — fix them and upload them again.</template>
      <template #append>
        <v-btn size="small" variant="text" append-icon="mdi-arrow-right" @click="openHistory">View in History</v-btn>
      </template>
    </v-alert>
    <v-divider />
    <div ref="tableArea" class="fill">
      <v-data-table-virtual :headers="headers" :items="visibleRows" item-value="line" class="consumable-table" density="compact"
                            fixed-header :height="tableHeight" no-data-text="No rows to show.">
        <template #item.status="{ item }">
          <v-chip size="small" :color="STATUS_COLORS[item.status]" variant="tonal" label>{{ item.status }}</v-chip>
        </template>
        <template #item.diaSpec="{ item }"><strong>{{ item.diaSpec || '—' }}</strong></template>
        <template #item.lot="{ item }">
          {{ item.lotNumber || '—' }}
          <div class="text-caption text-medium-emphasis">{{ item.brand }}</div>
        </template>
        <template #item.quantityKg="{ item }">{{ item.quantityKg != null ? kg(item.quantityKg) : '—' }}</template>
        <template #item.messages="{ item }">
          <div v-for="m in item.messages" :key="m" class="text-caption" :class="messageClass(item, m)">
            <v-icon v-if="m.startsWith(STANDARDIZED)" size="x-small" class="me-1">mdi-format-letter-case</v-icon>{{ m }}
          </div>
        </template>
      </v-data-table-virtual>
    </div>
    <v-divider />
    <v-card-actions class="px-4 py-3">
      <template v-if="!result.committed">
        <span class="text-body-2 text-medium-emphasis">{{ summary }}</span>
        <v-spacer />
        <v-btn variant="text" :disabled="busy === 'commit'" @click="reset">Cancel</v-btn>
        <v-btn v-if="result.rejected" color="warning" variant="tonal" :loading="busy === 'commit' && skipping"
               :disabled="!result.ready || busy === 'commit'" @click="commit(true)">
          Import {{ result.ready }} valid row(s) only
        </v-btn>
        <v-btn color="primary" variant="flat" prepend-icon="mdi-database-import" :loading="busy === 'commit' && !skipping"
               :disabled="!result.ready || result.rejected > 0 || busy === 'commit'" @click="commit(false)">
          Import {{ result.ready }} row(s)
        </v-btn>
      </template>
      <template v-else>
        <v-spacer />
        <v-btn color="primary" variant="flat" @click="reset">Done</v-btn>
      </template>
    </v-card-actions>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { computed, ref } from 'vue'
  import { useRouter } from 'vue-router'
  import { useFillHeight } from '@/composables/useFillHeight'
  import { useConsumableStore } from '@/store/consumableStore'
  import { stockGuide } from '@/utils/csvGuides'
  import { COLUMN, errorText, kg, saveBlob } from '@/utils/consumables'
  import CsvColumnGuide from '@/components/consumables/shared/CsvColumnGuide.vue'

  const OPENING = 'OPENING'
  const WARNING = 'Warning:'
  const STANDARDIZED = 'Standardized:'
  const STATUS_COLORS = { Ready: 'success', 'New consumable': 'primary', Error: 'error', Skipped: undefined }

  const store = useConsumableStore()
  const router = useRouter()
  const tableArea = ref(null)
  const tableHeight = useFillHeight(tableArea, 240)
  const fileInput = ref(null)
  const file = ref(null)
  const fileName = ref('')
  const result = ref(null)
  const problemsOnly = ref(false)
  const skipping = ref(false)
  const busy = ref('')
  const error = ref('')
  const guideOpen = ref(null)

  const guide = computed(() => stockGuide(store.catalog))

  const headers = [
    { title: 'Row', key: 'line', width: '64px' },
    { title: 'Status', key: 'status', width: '12%' },
    { title: COLUMN.diaSpec, key: 'diaSpec', width: '14%' },
    { title: 'Lot / Brand', key: 'lot', sortable: false, width: '14%' },
    { title: 'KG', key: 'quantityKg', align: 'end', width: '8%' },
    { title: 'Goes to', key: 'location', width: '12%' },
    { title: 'Details', key: 'messages', sortable: false, width: '34%' },
  ]

  const isProblem = (r) => r.status === 'Error' || r.messages.some((m) => m.startsWith(WARNING))
  const visibleRows = computed(() => (result.value?.rows ?? []).filter((r) => !problemsOnly.value || isProblem(r)))
  const totalKg = computed(() =>
    (result.value?.rows ?? []).filter((r) => r.status === 'Ready' || r.status === 'New consumable')
      .reduce((s, r) => s + (r.quantityKg ?? 0), 0))

  const summary = computed(() => {
    const r = result.value
    if (!r) return ''
    if (r.fileErrors.length && !r.rows.length) return 'Fix the columns and upload the file again.'
    if (!r.ready) return 'There are no valid rows to import.'
    return r.rejected
      ? 'Rows with errors are not imported. Fix the file and upload it again, or import only the valid rows.'
      : 'Check the rows, then import.'
  })

  function messageClass(row, m) {
    if (m.startsWith(STANDARDIZED)) return 'text-info'
    if (m.startsWith(WARNING)) return 'text-warning'
    return row.status === 'Error' ? 'text-error' : ''
  }

  async function downloadTemplate() {
    busy.value = 'template'
    error.value = ''
    try {
      saveBlob(await store.stockTemplate(), 'Opening stock template.csv')
    } catch (e) {
      error.value = errorText(e, 'Could not download the template.')
    } finally {
      busy.value = ''
    }
  }

  async function onFile(event) {
    const chosen = event.target.files?.[0]
    event.target.value = ''
    if (!chosen) return
    busy.value = 'preview'
    error.value = ''
    try {
      result.value = await store.importStock(chosen)
      file.value = chosen
      fileName.value = chosen.name
      problemsOnly.value = result.value.rejected > 0
      guideOpen.value = null
    } catch (e) {
      error.value = errorText(e, 'Could not read the file.')
    } finally {
      busy.value = ''
    }
  }

  async function commit(skipInvalid) {
    skipping.value = skipInvalid
    busy.value = 'commit'
    error.value = ''
    try {
      result.value = await store.importStock(file.value, { commit: true, skipInvalid })
      problemsOnly.value = !result.value.committed && result.value.rejected > 0
    } catch (e) {
      error.value = errorText(e, 'Import failed. Nothing was saved.')
    } finally {
      busy.value = ''
    }
  }

  function reset() {
    result.value = null
    file.value = null
    fileName.value = ''
    problemsOnly.value = false
  }

  function openHistory() {
    store.showHistory({ from: '', to: '', q: result.value.txnNo })
    router.push({ name: 'consumable-records' })
  }
</script>

<style scoped>
  .preview-card {
    min-height: 380px;
  }
</style>
