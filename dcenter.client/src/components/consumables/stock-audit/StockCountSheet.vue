<template>
  <v-card border flat class="fill-card">
    <v-card-text class="d-flex flex-wrap align-center ga-3">
      <v-btn-toggle v-model="scope" mandatory divided density="compact" variant="outlined" color="primary">
        <v-btn value="Activated" prepend-icon="mdi-view-grid-outline">Racks &amp; ovens</v-btn>
        <v-btn value="Normal" prepend-icon="mdi-warehouse">Main store</v-btn>
      </v-btn-toggle>
      <v-btn-toggle v-model="category" mandatory divided density="compact" variant="outlined" color="primary">
        <v-btn :value="ALL" size="small">All</v-btn>
        <v-btn v-for="c in store.catalog.categories" :key="c" :value="c" size="small">{{ c }}</v-btn>
      </v-btn-toggle>
      <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Filter lines" clearable v-bind="field"
                    style="max-width:240px;" />
      <v-switch v-model="differencesOnly" label="Differences only" color="warning" density="compact" hide-details inset />
      <v-spacer />
      <v-btn variant="text" prepend-icon="mdi-printer-outline" :disabled="!sheet" @click="printSheet">Print sheet</v-btn>
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="confirmReload">Reload</v-btn>
    </v-card-text>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mx-4 mb-3" closable @click:close="error = ''">
      {{ error }}
    </v-alert>
    <div v-if="sheet" class="px-4 pb-2 text-caption text-medium-emphasis">
      Sheet loaded {{ fmtDateTime(sheet.generatedAt) }} · {{ sheet.lines.length }} line(s) with stock.
      Counted values start at the system quantity — change only the lines that differ.
    </div>
    <v-divider />
    <div ref="tableArea" class="fill">
      <v-data-table-virtual :headers="headers" :items="visibleLines" :loading="loading" item-value="key" class="consumable-table"
                            density="compact" fixed-header :height="tableHeight"
                            :no-data-text="differencesOnly ? 'No differences entered.' : 'Nothing in this storage.'">
        <template #loading><v-skeleton-loader type="table-row@8" /></template>
        <template #item.location="{ item }">
          <v-chip size="small" label variant="tonal" :color="item.compartmentId ? 'indigo' : undefined">{{ item.location }}</v-chip>
        </template>
        <template #item.diaSpec="{ item }">
          <strong>{{ item.diaSpec }}</strong>
          <div class="text-caption text-medium-emphasis">{{ item.category }}</div>
        </template>
        <template #item.lotNumber="{ item }">
          {{ item.lotNumber }}
          <div class="text-caption text-medium-emphasis">{{ item.brand }}</div>
        </template>
        <template #item.systemKg="{ item }">{{ kg(item.systemKg) }}</template>
        <template #item.counted="{ item }">
          <v-text-field v-model.number="counted[item.key]" type="number" min="0" step="0.01" variant="outlined" density="compact"
                        hide-details suffix="kg" style="max-width:140px; margin-left:auto;" :aria-label="`Counted kg for lot ${item.lotNumber}`" />
        </template>
        <template #item.difference="{ item }">
          <span :class="diffClass(item)">{{ diffText(item) }}</span>
        </template>
      </v-data-table-virtual>
    </div>
    <v-divider />
    <v-card-text class="d-flex flex-wrap align-center ga-3">
      <v-text-field v-model="countDate" type="date" label="Count date" :max="todayIso()" v-bind="field" style="max-width:170px;" />
      <v-text-field v-model="remarks" label="Remarks" v-bind="field" class="flex-grow-1" style="min-width:200px;" />
      <span class="text-body-2">
        {{ changed.length }} difference(s) ·
        <span class="text-success">+{{ kg(gain) }}</span> /
        <span class="text-error">−{{ kg(loss) }}</span> kg
      </span>
      <v-btn color="primary" variant="flat" prepend-icon="mdi-check" :disabled="!canPost" @click="confirmOpen = true">Post count</v-btn>
    </v-card-text>
  </v-card>

  <v-dialog v-model="confirmOpen" max-width="560">
    <v-card prepend-icon="mdi-clipboard-check-outline" title="Post stock count?">
      <v-card-text>
        <p class="text-body-2 mb-2">
          {{ lines.length }} line(s) counted in {{ scope === 'Normal' ? 'the main store' : 'racks and ovens' }}.
          <template v-if="changed.length">
            {{ changed.length }} difference(s) will be posted as adjustments (gain {{ kg(gain) }} kg, loss {{ kg(loss) }} kg)
            under one reference number.
          </template>
          <template v-else>No differences — the count is recorded with no adjustments.</template>
        </p>
        <v-alert v-if="blankCount" type="warning" variant="tonal" density="compact">
          {{ blankCount }} line(s) have no counted value and are treated as unchanged.
        </v-alert>
        <v-alert v-if="postError" type="error" variant="tonal" density="compact" class="mt-3">{{ postError }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="posting" @click="confirmOpen = false">Back</v-btn>
        <v-btn color="primary" variant="flat" :loading="posting" @click="post">Post</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="reloadOpen" max-width="440">
    <v-card title="Discard entered counts?" text="Reloading replaces the sheet and clears the differences you have entered.">
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="reloadOpen = false">Keep</v-btn>
        <v-btn color="warning" variant="flat" @click="reloadOpen = false; load()">Reload</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
  <v-snackbar v-model="snackbar" color="success" timeout="5000">{{ snackbarText }}</v-snackbar>
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { computed, onMounted, ref, watch } from 'vue'
  import { useFillHeight } from '@/composables/useFillHeight'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { ALL, COLUMN, errorText, fmtDateTime, kg, openPrint, todayIso } from '@/utils/consumables'

  const tableArea = ref(null)
  const tableHeight = useFillHeight(tableArea)
  const emit = defineEmits(['posted'])

  const store = useConsumableStore()
  const router = useRouter()
  const field = { variant: 'outlined', density: 'compact', hideDetails: true }
  const scope = ref('Activated')
  const category = ref(ALL)
  const search = ref('')
  const differencesOnly = ref(false)
  const sheet = ref(null)
  const counted = ref({})
  const loading = ref(false)
  const error = ref('')
  const countDate = ref(todayIso())
  const remarks = ref('')
  const confirmOpen = ref(false)
  const reloadOpen = ref(false)
  const posting = ref(false)
  const postError = ref('')
  const snackbar = ref(false)
  const snackbarText = ref('')

  const headers = [
    { title: 'Location', key: 'location', width: '12%' },
    { title: 'Consumable', key: 'diaSpec', width: '26%' },
    { title: COLUMN.lot, key: 'lotNumber', width: '18%' },
    { title: 'System (KG)', key: 'systemKg', align: 'end', width: '12%' },
    { title: 'Counted (KG)', key: 'counted', sortable: false, align: 'end', width: '18%' },
    { title: 'Difference', key: 'difference', sortable: false, align: 'end', width: '14%' },
  ]

  const lines = computed(() => sheet.value?.lines ?? [])
  const round = (v) => Math.round(v * 100) / 100
  const valueOf = (line) => {
    const v = counted.value[line.key]
    return typeof v === 'number' && v >= 0 ? round(v) : null
  }
  const differenceOf = (line) => {
    const v = valueOf(line)
    return v === null ? 0 : round(v - line.systemKg)
  }
  const changed = computed(() => lines.value.filter((l) => differenceOf(l) !== 0))
  const gain = computed(() => changed.value.reduce((s, l) => s + Math.max(differenceOf(l), 0), 0))
  const loss = computed(() => changed.value.reduce((s, l) => s + Math.max(-differenceOf(l), 0), 0))
  const blankCount = computed(() => lines.value.filter((l) => valueOf(l) === null).length)
  const canPost = computed(() => !!sheet.value && lines.value.length > 0 && store.hasEnteredBy && !!countDate.value)

  const visibleLines = computed(() => {
    const term = (search.value ?? '').trim().toLowerCase()
    return lines.value.filter((l) =>
      (!differencesOnly.value || differenceOf(l) !== 0)
      && (!term || [l.location, l.diaSpec, l.lotNumber, l.brand].some((v) => (v ?? '').toLowerCase().includes(term))))
  })

  function diffText(line) {
    const d = differenceOf(line)
    return d > 0 ? `+${kg(d)}` : d < 0 ? kg(d) : '—'
  }

  function diffClass(line) {
    const d = differenceOf(line)
    return d > 0 ? 'text-success font-weight-bold' : d < 0 ? 'text-error font-weight-bold' : 'text-medium-emphasis'
  }

  async function load() {
    loading.value = true
    error.value = ''
    try {
      const data = await store.loadCountSheet(scope.value, category.value)
      sheet.value = data
      counted.value = Object.fromEntries(data.lines.map((l) => [l.key, l.systemKg]))
    } catch (e) {
      error.value = errorText(e, 'Could not load the count sheet.')
    } finally {
      loading.value = false
    }
  }

  function confirmReload() {
    if (changed.value.length) reloadOpen.value = true
    else load()
  }

  function printSheet() {
    openPrint(router, 'count-sheet', { scope: scope.value, category: category.value === ALL ? null : category.value })
  }

  async function post() {
    posting.value = true
    postError.value = ''
    try {
      const result = await store.postStockCount({
        countDate: countDate.value,
        scope: sheet.value.scope,
        category: sheet.value.category,
        remarks: remarks.value.trim() || null,
        lines: lines.value.map((l) => ({
          itemId: l.itemId,
          lotId: l.lotId,
          compartmentId: l.compartmentId,
          systemKg: l.systemKg,
          countedKg: valueOf(l) ?? l.systemKg,
        })),
      })
      confirmOpen.value = false
      snackbarText.value = `${result.referenceNo} posted — ${result.linesAdjusted} adjustment(s).`
      snackbar.value = true
      remarks.value = ''
      await load()
      emit('posted', result)
    } catch (e) {
      postError.value = errorText(e, 'Could not post the stock count.')
    } finally {
      posting.value = false
    }
  }

  watch([scope, category], load)
  onMounted(async () => {
    await store.loadCatalog()
    await load()
  })
</script>
