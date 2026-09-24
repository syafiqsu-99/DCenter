<template>
  <v-card border flat>
    <div class="d-flex align-center px-4 py-2 text-body-2 text-medium-emphasis">
      {{ total }} line(s)
      <v-spacer />
      <v-btn variant="text" size="small" prepend-icon="mdi-file-delimited-outline" :disabled="!items.length" @click="exportCsv">
        Export loaded rows
      </v-btn>
    </div>
    <v-divider />
    <v-data-table-virtual :headers="headers" :items="items" :loading="loading" item-value="id" class="consumable-table"
                          density="compact" fixed-header height="calc(100vh - 380px)"
                          no-data-text="No transactions match the filters.">
      <template #loading><v-skeleton-loader type="table-row@8" /></template>
      <template #item.txnNo="{ item }"><span class="text-no-wrap">{{ item.txnNo }}</span></template>
      <template #item.txnDate="{ item }">{{ fmtDate(item.txnDate) }}</template>
      <template #item.txnType="{ item }"><TxnTypeChip :type="item.txnType" :voided="item.isVoided" /></template>
      <template #item.flow="{ item }"><span class="text-no-wrap">{{ stageFlow(item) }}</span></template>
      <template #item.diaSpec="{ item }">
        {{ item.diaSpec }}
        <div class="text-caption text-medium-emphasis">
          {{ item.brand }} · Lot {{ item.lotNumber }}<template v-if="item.bakingNo"> · {{ item.bakingNo }}</template>
        </div>
      </template>
      <template #item.quantityKg="{ item }">
        <span :class="{ 'text-disabled text-decoration-line-through': item.isVoided }">{{ kg(item.quantityKg) }}</span>
      </template>
      <template #item.who="{ item }">{{ item.welderName || item.requestor || '—' }}</template>
      <template #item.detail="{ item }">
        <span v-if="item.reason">{{ item.reason }}<template v-if="item.countedQtyKg !== null"> · counted {{ kg(item.countedQtyKg) }}</template></span>
        <div v-if="item.remarks" class="text-caption text-medium-emphasis">{{ item.remarks }}</div>
      </template>
      <template #item.created="{ item }">
        <span class="text-caption">{{ fmtDateTime(item.createdAt) }}<br>{{ item.createdBy || '—' }}</span>
      </template>
      <template #item.actions="{ item }">
        <v-btn v-if="item.txnType !== 'Void' && !item.isVoided" size="small" variant="text" color="error" @click="askVoid(item)">
          Void
        </v-btn>
      </template>
    </v-data-table-virtual>
    <div v-if="items.length < total" class="d-flex justify-center py-2">
      <v-btn variant="tonal" :loading="loadingMore" @click="loadMore">Load more ({{ total - items.length }} left)</v-btn>
    </div>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-3">{{ error }}</v-alert>
  </v-card>

  <VoidDialog v-model="voidOpen" :transaction="voidTarget" @voided="reload" />
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { onMounted, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, TXN_LABELS, categoryParam, debounce, downloadCsv, errorText, fmtDate, fmtDateTime, kg, stageFlow, todayIso } from '@/utils/consumables'
  import TxnTypeChip from '@/components/consumables/TxnTypeChip.vue'
  import VoidDialog from '@/components/consumables/VoidDialog.vue'

  const PAGE_SIZE = 100

  const store = useConsumableStore()
  const items = ref([])
  const total = ref(0)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref('')
  const voidOpen = ref(false)
  const voidTarget = ref(null)
  let token = 0

  const headers = [
    { title: 'Txn No.', key: 'txnNo', sortable: false, width: '9%' },
    { title: COLUMN.date, key: 'txnDate', sortable: false, width: '7%' },
    { title: 'Type', key: 'txnType', sortable: false, width: '8%' },
    { title: 'From → To', key: 'flow', sortable: false, width: '11%' },
    { title: 'Consumable', key: 'diaSpec', sortable: false, width: '17%' },
    { title: 'KG', key: 'quantityKg', sortable: false, align: 'end', width: '6%' },
    { title: 'Welder / Requestor', key: 'who', sortable: false, width: '11%' },
    { title: 'Reason / Remarks', key: 'detail', sortable: false, width: '14%' },
    { title: 'Entered', key: 'created', sortable: false, width: '11%' },
    { title: '', key: 'actions', sortable: false, align: 'end', width: '6%' },
  ]

  function params(skip) {
    const f = store.historyFilters
    return {
      from: f.from || undefined,
      to: f.to || undefined,
      type: f.type || undefined,
      stage: f.stage || undefined,
      category: categoryParam(f.category),
      compartmentId: f.compartmentId || undefined,
      welderId: f.welder?.id || undefined,
      q: (f.q ?? '').trim() || undefined,
      skip,
      take: PAGE_SIZE,
    }
  }

  async function reload() {
    const current = ++token
    loading.value = true
    error.value = ''
    try {
      const page = await store.loadTransactions(params(0))
      if (current !== token) return
      items.value = page.items
      total.value = page.total
    } catch (e) {
      if (current === token) error.value = errorText(e, 'Could not load transactions.')
    } finally {
      if (current === token) loading.value = false
    }
  }

  async function loadMore() {
    loadingMore.value = true
    try {
      const page = await store.loadTransactions(params(items.value.length))
      items.value = [...items.value, ...page.items]
      total.value = page.total
    } catch (e) {
      error.value = errorText(e, 'Could not load more transactions.')
    } finally {
      loadingMore.value = false
    }
  }

  function askVoid(item) {
    voidTarget.value = item
    voidOpen.value = true
  }

  function exportCsv() {
    const header = ['Txn No.', COLUMN.date, 'Type', 'From', 'From compartment', 'To', 'To compartment', 'Baking No', COLUMN.type, COLUMN.diaSpec, COLUMN.brand, COLUMN.lot, 'KG',
      COLUMN.source, 'Welder / Requestor', 'Reason', 'Counted (KG)', 'Remarks', 'Voided', 'Entered at', 'Entered by']
    const rows = items.value.map((t) => [t.txnNo, fmtDate(t.txnDate), TXN_LABELS[t.txnType] ?? t.txnType, t.fromStage ?? '',
      t.fromCompartment ?? '', t.toStage ?? '', t.toCompartment ?? '', t.bakingNo ?? '', t.category, t.diaSpec, t.brand, t.lotNumber, Number(t.quantityKg).toFixed(2), t.source ?? '',
      t.welderName || t.requestor || '', t.reason ?? '', t.countedQtyKg ?? '', t.remarks ?? '', t.isVoided ? 'Yes' : '',
      fmtDateTime(t.createdAt), t.createdBy ?? ''])
    downloadCsv(`Consumable transactions ${todayIso()}.csv`, [header, ...rows])
  }

  const debouncedReload = debounce(reload, 300)
  watch(() => ({ ...store.historyFilters }), debouncedReload, { deep: true })
  onMounted(reload)
</script>
