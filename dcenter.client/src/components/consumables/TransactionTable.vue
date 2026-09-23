<template>
  <v-card border flat>
    <v-card-title class="d-flex align-center text-subtitle-2">
      {{ total }} transaction(s)
      <span v-if="items.length < total" class="text-medium-emphasis ms-1">· showing {{ items.length }}, scroll for more</span>
      <v-spacer />
      <v-progress-circular v-if="loadingMore" indeterminate size="18" width="2" />
    </v-card-title>
    <v-divider />
    <div ref="tableArea">
      <v-data-table-virtual :headers="headers" :items="items" :loading="loading" item-value="id" class="consumable-table"
                            density="compact" hover fixed-header height="calc(100vh - 380px)" style="min-height:360px;"
                            no-data-text="No transactions for these filters.">
        <template #loading>
          <v-skeleton-loader type="table-row@8" />
        </template>
        <template #item.txnDate="{ item }">{{ fmtDate(item.txnDate) }}</template>
        <template #item.txnType="{ item }"><TxnTypeChip :type="item.txnType" :voided="item.isVoided" /></template>
        <template #item.quantityKg="{ item }">
          <span :class="item.isVoided ? 'text-disabled text-decoration-line-through' : ''">
            {{ item.quantityKg > 0 ? '+' : '' }}{{ kg(item.quantityKg) }}
          </span>
        </template>
        <template #item.createdAt="{ item }">
          <span class="text-caption">{{ fmtDateTime(item.createdAt) }}</span>
        </template>
        <template #item.actions="{ item }">
          <v-tooltip v-if="item.txnType !== 'Void' && !item.isVoided" location="top"
                     text="Cancel this entry (kept in history with your reason)">
            <template #activator="{ props: p }">
              <v-btn v-bind="p" size="small" variant="text" color="error" @click="askVoid(item)">Void</v-btn>
            </template>
          </v-tooltip>
        </template>
      </v-data-table-virtual>
    </div>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-3">{{ error }}</v-alert>
  </v-card>

  <VoidDialog v-model="voidOpen" :transaction="voidTarget" @voided="reload" />
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, debounce, errorText, fmtDate, fmtDateTime, kg } from '@/utils/consumables'
  import TxnTypeChip from '@/components/consumables/TxnTypeChip.vue'
  import VoidDialog from '@/components/consumables/VoidDialog.vue'

  const PAGE_SIZE = 100

  const store = useConsumableStore()
  const tableArea = ref(null)
  const items = ref([])
  const total = ref(0)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref('')
  const voidOpen = ref(false)
  const voidTarget = ref(null)
  let token = 0
  let scrollEl = null

  const headers = [
    { title: COLUMN.date, key: 'txnDate', sortable: false, width: '7%' },
    { title: 'Type', key: 'txnType', sortable: false, width: '7%' },
    { title: COLUMN.source, key: 'location', sortable: false, width: '7%' },
    { title: COLUMN.brand, key: 'manufacturer', sortable: false, width: '10%' },
    { title: COLUMN.diaSpec, key: 'diaSpec', sortable: false, width: '13%' },
    { title: COLUMN.lot, key: 'lotNumber', sortable: false, width: '9%' },
    { title: 'KG', key: 'quantityKg', sortable: false, align: 'end', width: '6%' },
    { title: COLUMN.requestor, key: 'requestor', sortable: false, width: '10%' },
    { title: 'Reference', key: 'referenceNo', sortable: false, width: '7%' },
    { title: 'Remarks', key: 'remarks', sortable: false, width: '11%' },
    { title: 'Entered', key: 'createdAt', sortable: false, width: '8%' },
    { title: '', key: 'actions', sortable: false, align: 'end', width: '5%' },
  ]

  function params(skip) {
    const f = store.historyFilters
    return {
      from: f.from || undefined,
      to: f.to || undefined,
      type: f.type || undefined,
      location: f.location,
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
      const data = await store.loadTransactions(params(0))
      if (current !== token) return
      items.value = data.items
      total.value = data.total
      if (scrollEl) scrollEl.scrollTop = 0
    } catch (e) {
      if (current === token) error.value = errorText(e, 'Could not load transactions.')
    } finally {
      if (current === token) loading.value = false
    }
  }

  async function loadMore() {
    if (loading.value || loadingMore.value || items.value.length >= total.value) return
    const current = token
    loadingMore.value = true
    try {
      const data = await store.loadTransactions(params(items.value.length))
      if (current !== token) return
      items.value.push(...data.items)
      total.value = data.total
    } catch {
      if (current === token) error.value = 'Could not load more transactions.'
    } finally {
      loadingMore.value = false
    }
  }

  function onScroll() {
    if (!scrollEl) return
    if (scrollEl.scrollHeight - scrollEl.scrollTop - scrollEl.clientHeight < 200) loadMore()
  }

  const debouncedReload = debounce(reload, 300)
  watch(() => ({ ...store.historyFilters }), debouncedReload, { deep: true })

  function askVoid(item) {
    voidTarget.value = item
    voidOpen.value = true
  }

  onMounted(async () => {
    await nextTick()
    scrollEl = tableArea.value?.querySelector('.v-table__wrapper') ?? null
    scrollEl?.addEventListener('scroll', onScroll, { passive: true })
    reload()
  })

  onBeforeUnmount(() => scrollEl?.removeEventListener('scroll', onScroll))
</script>
