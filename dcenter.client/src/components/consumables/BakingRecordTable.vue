<template>
  <v-card border flat>
    <v-card-text class="d-flex flex-wrap align-center ga-3">
      <v-text-field v-model="filters.from" type="date" label="From" v-bind="field" style="max-width:170px;" />
      <v-text-field v-model="filters.to" type="date" label="To" v-bind="field" style="max-width:170px;" />
      <v-select v-model="filters.status" :items="statusItems" item-title="title" item-value="value" label="Status" clearable
                v-bind="field" style="max-width:190px;" />
      <v-text-field v-model="filters.q" prepend-inner-icon="mdi-magnify" label="Baking no., lot, PIC, spec…" clearable
                    v-bind="field" class="flex-grow-1" style="min-width:220px;" />
      <v-btn variant="text" prepend-icon="mdi-printer-outline" :disabled="!items.length" @click="printRecords">Print</v-btn>
      <v-btn variant="text" prepend-icon="mdi-file-delimited-outline" :disabled="!items.length" @click="exportCsv">Export</v-btn>
    </v-card-text>
    <v-divider />
    <v-data-table-virtual :headers="headers" :items="items" :loading="loading" item-value="id" class="consumable-table"
                          density="compact" fixed-header height="calc(100vh - 400px)" no-data-text="No baking records.">
      <template #loading><v-skeleton-loader type="table-row@8" /></template>
      <template #item.bakingDate="{ item }">{{ fmtDate(item.bakingDate) }}</template>
      <template #item.diaSpec="{ item }">
        {{ item.diaSpec }}
        <div class="text-caption text-medium-emphasis">{{ item.brand }} · Lot {{ item.lotNumber }}</div>
      </template>
      <template #item.quantityKg="{ item }">{{ kg(item.quantityKg) }}</template>
      <template #item.balanceKg="{ item }">{{ item.balanceKg ? kg(item.balanceKg) : '—' }}</template>
      <template #item.bake="{ item }">
        <span class="text-caption">{{ fmtDateTime(item.bakeStart) || '—' }}<br>{{ fmtDateTime(item.bakeStop) || '—' }}</span>
      </template>
      <template #item.rebake="{ item }">
        <span class="text-caption">{{ fmtDateTime(item.rebakeStart) || '—' }}<br>{{ fmtDateTime(item.rebakeStop) || '—' }}</span>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="BAKING_COLORS[item.status]" variant="tonal" label>{{ BAKING_LABELS[item.status] }}</v-chip>
      </template>
      <template #item.actions="{ item }">
        <v-btn icon="mdi-history" size="small" variant="text" :aria-label="`Movements of ${item.bakingNo}`" @click="openHistory(item)" />
      </template>
    </v-data-table-virtual>
    <div v-if="items.length < total" class="d-flex justify-center py-2">
      <v-btn variant="tonal" :loading="loadingMore" @click="loadMore">Load more ({{ total - items.length }} left)</v-btn>
    </div>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-3">{{ error }}</v-alert>
  </v-card>

  <v-dialog v-model="historyOpen" max-width="900">
    <v-card v-if="historyRecord" prepend-icon="mdi-history" :title="`${historyRecord.bakingNo} movements`"
            :subtitle="`${historyRecord.diaSpec} · Lot ${historyRecord.lotNumber}`">
      <v-divider />
      <v-data-table-virtual :headers="historyHeaders" :items="historyItems" :loading="historyLoading" item-value="id"
                            class="consumable-table" density="compact" height="380" no-data-text="No movements.">
        <template #item.txnDate="{ item }">{{ fmtDate(item.txnDate) }}</template>
        <template #item.txnType="{ item }"><TxnTypeChip :type="item.txnType" :voided="item.isVoided" /></template>
        <template #item.flow="{ item }">{{ stageFlow(item) }}</template>
        <template #item.quantityKg="{ item }">{{ kg(item.quantityKg) }}</template>
        <template #item.who="{ item }">{{ item.welderName || '—' }}</template>
      </v-data-table-virtual>
      <v-card-actions><v-spacer /><v-btn variant="text" @click="historyOpen = false">Close</v-btn></v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { onMounted, reactive, ref, watch } from 'vue'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { BAKING_COLORS, BAKING_LABELS, COLUMN, debounce, downloadCsv, errorText, fmtDate, fmtDateTime, kg, monthStartIso, openPrint, stageFlow, todayIso } from '@/utils/consumables'
  import TxnTypeChip from '@/components/consumables/TxnTypeChip.vue'

  const PAGE_SIZE = 100

  const store = useConsumableStore()
  const router = useRouter()
  const field = { variant: 'outlined', density: 'compact', hideDetails: true }
  const filters = reactive({ from: monthStartIso(), to: todayIso(), status: null, q: '' })
  const statusItems = Object.entries(BAKING_LABELS).map(([value, title]) => ({ value, title }))
  const items = ref([])
  const total = ref(0)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref('')
  const historyOpen = ref(false)
  const historyRecord = ref(null)
  const historyItems = ref([])
  const historyLoading = ref(false)
  let token = 0

  const headers = [
    { title: 'Baking No', key: 'bakingNo', width: '9%' },
    { title: 'Baking Date', key: 'bakingDate', width: '8%' },
    { title: 'Consumable', key: 'diaSpec', width: '18%' },
    { title: 'KG', key: 'quantityKg', align: 'end', width: '6%' },
    { title: 'Unplaced', key: 'balanceKg', align: 'end', width: '7%' },
    { title: 'Person In Charge', key: 'personInCharge', width: '11%' },
    { title: 'Start / Stop', key: 'bake', sortable: false, width: '12%' },
    { title: 'Re-Bake Start / Stop', key: 'rebake', sortable: false, width: '12%' },
    { title: 'Status', key: 'status', width: '9%' },
    { title: 'Remarks', key: 'remarks', width: '5%' },
    { title: '', key: 'actions', sortable: false, align: 'end', width: '3%' },
  ]

  const historyHeaders = [
    { title: 'Txn No.', key: 'txnNo' },
    { title: COLUMN.date, key: 'txnDate' },
    { title: 'Type', key: 'txnType' },
    { title: 'From → To', key: 'flow' },
    { title: 'KG', key: 'quantityKg', align: 'end' },
    { title: COLUMN.welder, key: 'who' },
    { title: 'Entered by', key: 'createdBy' },
  ]

  function params(skip) {
    return {
      from: filters.from || undefined,
      to: filters.to || undefined,
      status: filters.status || undefined,
      q: (filters.q ?? '').trim() || undefined,
      skip,
      take: PAGE_SIZE,
    }
  }

  async function reload() {
    const current = ++token
    loading.value = true
    error.value = ''
    try {
      const page = await store.loadBakingRecords(params(0))
      if (current !== token) return
      items.value = page.items
      total.value = page.total
    } catch (e) {
      if (current === token) error.value = errorText(e, 'Could not load baking records.')
    } finally {
      if (current === token) loading.value = false
    }
  }

  async function loadMore() {
    loadingMore.value = true
    try {
      const page = await store.loadBakingRecords(params(items.value.length))
      items.value = [...items.value, ...page.items]
      total.value = page.total
    } catch (e) {
      error.value = errorText(e, 'Could not load more records.')
    } finally {
      loadingMore.value = false
    }
  }

  async function openHistory(record) {
    historyRecord.value = record
    historyOpen.value = true
    historyLoading.value = true
    historyItems.value = []
    try {
      const page = await store.loadTransactions({ bakingRecordId: record.id, take: 200 })
      historyItems.value = page.items
    } catch (e) {
      error.value = errorText(e, 'Could not load the movements.')
    } finally {
      historyLoading.value = false
    }
  }

  function printRecords() {
    openPrint(router, 'baking-records', { from: filters.from, to: filters.to, status: filters.status, q: (filters.q ?? '').trim() })
  }

  function exportCsv() {
    const header = ['Baking No', 'Baking Date', COLUMN.type, COLUMN.diaSpec, COLUMN.brand, COLUMN.lot, 'Quantity (KG)',
      'Person In Charge', 'Start Time', 'Stop Time', 'Re-Baking Start', 'Re-Baking Stop', 'Status', 'Remarks']
    const rows = items.value.map((r) => [r.bakingNo, fmtDate(r.bakingDate), r.category, r.diaSpec, r.brand, r.lotNumber,
      Number(r.quantityKg).toFixed(2), r.personInCharge, fmtDateTime(r.bakeStart), fmtDateTime(r.bakeStop),
      fmtDateTime(r.rebakeStart), fmtDateTime(r.rebakeStop), BAKING_LABELS[r.status], r.remarks ?? ''])
    downloadCsv(`Baking records ${todayIso()}.csv`, [header, ...rows])
  }

  watch(() => ({ ...filters }), debounce(reload, 300), { deep: true })
  onMounted(reload)
</script>
