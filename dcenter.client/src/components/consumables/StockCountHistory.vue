<template>
  <v-card border flat class="fill-card">
    <v-card-text class="d-flex flex-wrap align-center ga-3">
      <v-text-field v-model="filters.from" type="date" label="From" v-bind="field" style="max-width:170px;" />
      <v-text-field v-model="filters.to" type="date" label="To" v-bind="field" style="max-width:170px;" />
      <v-select v-model="filters.scope" :items="scopeItems" item-title="title" item-value="value" label="Storage" clearable
                v-bind="field" style="max-width:190px;" />
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="reload">Refresh</v-btn>
    </v-card-text>
    <v-divider />
    <div ref="tableArea" class="fill">
      <v-data-table-virtual :headers="headers" :items="items" :loading="loading" item-value="id" class="consumable-table"
                            density="compact" fixed-header :height="tableHeight" no-data-text="No stock counts yet.">
        <template #loading><v-skeleton-loader type="table-row@8" /></template>
        <template #item.referenceNo="{ item }">
          <span :class="{ 'text-decoration-line-through text-disabled': item.isVoided }">{{ item.referenceNo }}</span>
          <v-chip v-if="item.isVoided" size="x-small" color="error" variant="tonal" class="ms-1">Voided</v-chip>
        </template>
        <template #item.countDate="{ item }">{{ fmtDate(item.countDate) }}</template>
        <template #item.scope="{ item }">
          {{ item.scope === 'Normal' ? 'Main store' : 'Racks & ovens' }}
          <div class="text-caption text-medium-emphasis">{{ item.category || 'All types' }}</div>
        </template>
        <template #item.lines="{ item }">{{ item.linesAdjusted }} / {{ item.linesCounted }}</template>
        <template #item.gainKg="{ item }"><span class="text-success">{{ item.gainKg ? `+${kg(item.gainKg)}` : '—' }}</span></template>
        <template #item.lossKg="{ item }"><span class="text-error">{{ item.lossKg ? `−${kg(item.lossKg)}` : '—' }}</span></template>
        <template #item.netKg="{ item }"><strong>{{ kg(item.netKg) }}</strong></template>
        <template #item.created="{ item }">
          <span class="text-caption">{{ item.createdBy }}<br>{{ fmtDateTime(item.createdAt) }}</span>
        </template>
        <template #item.actions="{ item }">
          <div class="d-flex justify-end">
            <v-btn icon="mdi-printer-outline" size="small" variant="text" :aria-label="`Print ${item.referenceNo}`"
                   @click="openPrint(router, 'count-result', { ref: item.referenceNo })" />
            <v-btn v-if="item.txnNo && !item.isVoided" icon="mdi-cancel" size="small" variant="text" color="error"
                   :aria-label="`Void ${item.referenceNo}`" @click="openVoid(item)" />
          </div>
        </template>
      </v-data-table-virtual>
    </div>
    <div v-if="items.length < total" class="d-flex justify-center py-2">
      <v-btn variant="tonal" :loading="loadingMore" @click="loadMore">Load more ({{ total - items.length }} left)</v-btn>
    </div>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-3">{{ error }}</v-alert>
  </v-card>

  <VoidDialog v-model="voidOpen" :transaction="voidTarget" @voided="reload" />
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { onMounted, reactive, ref, watch } from 'vue'
  import { useFillHeight } from '@/composables/useFillHeight'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { debounce, errorText, fmtDate, fmtDateTime, kg, openPrint } from '@/utils/consumables'
  import VoidDialog from '@/components/consumables/VoidDialog.vue'

  const tableArea = ref(null)
  const tableHeight = useFillHeight(tableArea)
  const PAGE_SIZE = 100

  const store = useConsumableStore()
  const router = useRouter()
  const field = { variant: 'outlined', density: 'compact', hideDetails: true }
  const filters = reactive({ from: '', to: '', scope: null })
  const scopeItems = [{ title: 'Racks & ovens', value: 'Activated' }, { title: 'Main store', value: 'Normal' }]
  const items = ref([])
  const total = ref(0)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref('')
  const voidOpen = ref(false)
  const voidTarget = ref(null)
  let token = 0

  const headers = [
    { title: 'Reference', key: 'referenceNo', width: '13%' },
    { title: 'Count date', key: 'countDate', width: '9%' },
    { title: 'Storage', key: 'scope', width: '14%' },
    { title: 'Adjusted / counted', key: 'lines', sortable: false, align: 'end', width: '11%' },
    { title: 'Gain (KG)', key: 'gainKg', align: 'end', width: '8%' },
    { title: 'Loss (KG)', key: 'lossKg', align: 'end', width: '8%' },
    { title: 'Net (KG)', key: 'netKg', align: 'end', width: '8%' },
    { title: 'Remarks', key: 'remarks', width: '13%' },
    { title: 'Counted by', key: 'created', sortable: false, width: '10%' },
    { title: '', key: 'actions', sortable: false, align: 'end', width: '6%' },
  ]

  function params(skip) {
    return { from: filters.from || undefined, to: filters.to || undefined, scope: filters.scope || undefined, skip, take: PAGE_SIZE }
  }

  async function reload() {
    const current = ++token
    loading.value = true
    error.value = ''
    try {
      const page = await store.loadStockCounts(params(0))
      if (current !== token) return
      items.value = page.items
      total.value = page.total
    } catch (e) {
      if (current === token) error.value = errorText(e, 'Could not load stock counts.')
    } finally {
      if (current === token) loading.value = false
    }
  }

  async function loadMore() {
    loadingMore.value = true
    try {
      const page = await store.loadStockCounts(params(items.value.length))
      items.value = [...items.value, ...page.items]
      total.value = page.total
    } catch (e) {
      error.value = errorText(e, 'Could not load more counts.')
    } finally {
      loadingMore.value = false
    }
  }

  function openVoid(count) {
    voidTarget.value = { txnNo: count.txnNo, txnType: 'Adjust', diaSpec: `stock count ${count.referenceNo}` }
    voidOpen.value = true
  }

  watch(() => ({ ...filters }), debounce(reload, 300), { deep: true })
  onMounted(reload)
</script>
