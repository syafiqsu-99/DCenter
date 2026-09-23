<template>
  <v-card border flat>
    <v-data-table-virtual :headers="headers" :items="store.filteredCard" :loading="store.loadingStockCard"
                          :item-value="(r) => `${r.lotId}|${r.source}`" density="compact" hover fixed-header
                          height="calc(100vh - 400px)" :sort-by="[{ key: 'date', order: 'desc' }]"
                          no-data-text="No stock matches these filters." class="stock-card consumable-table" style="min-height:360px;">
      <template #loading>
        <v-skeleton-loader type="table-row@8" />
      </template>
      <template #item.date="{ item }">{{ fmtDate(item.date) }}</template>
      <template #item.diaSpec="{ item }">
        <span class="bg-grey-lighten-3 px-1 rounded">{{ item.diaSpec }}</span>
      </template>
      <template #item.receiveQtyKg="{ item }">{{ kg(item.receiveQtyKg) }}</template>
      <template #item.takes="{ item }">
        <div class="d-flex flex-wrap ga-1 py-1">
          <v-tooltip v-for="t in item.takes.slice(0, 7)" :key="t.id" location="top">
            <template #activator="{ props: p }">
              <v-chip v-bind="p" size="x-small" variant="outlined">{{ kg(t.quantityKg) }}</v-chip>
            </template>
            {{ fmtDate(t.txnDate) }} · {{ t.requestor || '—' }}
          </v-tooltip>
          <v-tooltip v-if="item.takes.length > 7" location="top" text="Show all takes for this lot">
            <template #activator="{ props: p }">
              <v-chip v-bind="p" size="x-small" variant="tonal" @click="openHistory(item)">
                +{{ item.takes.length - 7 }}
              </v-chip>
            </template>
          </v-tooltip>
        </div>
      </template>
      <template #item.balanceKg="{ item }">
        <span class="font-weight-bold" :class="{ 'text-warning': item.isLow }">{{ kg(item.balanceKg) }}</span>
        <v-tooltip v-if="item.isLow" location="top" :text="`Low stock — minimum ${kg(item.minStockKg)} kg across all lots`">
          <template #activator="{ props: p }">
            <v-icon v-bind="p" color="warning" size="small" class="ms-1">mdi-alert</v-icon>
          </template>
        </v-tooltip>
      </template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end">
          <v-tooltip location="top" :text="item.balanceKg > 0 ? 'Stock out from this lot' : 'No balance left to stock out'">
            <template #activator="{ props: p }">
              <span v-bind="p">
                <v-btn icon="mdi-tray-arrow-up" variant="text" size="small" :disabled="item.balanceKg <= 0"
                       :aria-label="`Stock out from lot ${item.lotNumber}`" @click="issue(item)" />
              </span>
            </template>
          </v-tooltip>
          <v-tooltip location="top" text="View stock in / out history of this lot">
            <template #activator="{ props: p }">
              <v-btn v-bind="p" icon="mdi-history" variant="text" size="small"
                     :aria-label="`History of lot ${item.lotNumber}`" @click="openHistory(item)" />
            </template>
          </v-tooltip>
        </div>
      </template>
    </v-data-table-virtual>
  </v-card>

  <LotHistoryDialog v-model="historyOpen" :row="selected" />
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { ref } from 'vue'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, fmtDate, kg } from '@/utils/consumables'
  import LotHistoryDialog from '@/components/consumables/LotHistoryDialog.vue'

  const store = useConsumableStore()
  const router = useRouter()
  const selected = ref(null)
  const historyOpen = ref(false)

  const headers = [
    { title: COLUMN.requestor, key: 'requestor', width: '8%' },
    { title: COLUMN.date, key: 'date', width: '7%' },
    { title: COLUMN.source, key: 'source', width: '7%' },
    { title: COLUMN.brand, key: 'brand', width: '9%' },
    { title: COLUMN.diameter, key: 'diameter', width: '7%' },
    { title: COLUMN.spec, key: 'specification', width: '10%' },
    { title: COLUMN.lot, key: 'lotNumber', width: '8%' },
    { title: COLUMN.diaSpec, key: 'diaSpec', width: '11%' },
    { title: COLUMN.receiveQty, key: 'receiveQtyKg', width: '7%', align: 'end' },
    { title: COLUMN.take, key: 'takes', sortable: false, width: '12%' },
    { title: COLUMN.balance, key: 'balanceKg', width: '7%', align: 'end' },
    { title: '', key: 'actions', sortable: false, width: '7%', align: 'end' },
  ]

  function issue(row) {
    router.push({ name: 'consumable-issue', query: { location: row.source, lotId: row.lotId } })
  }

  function openHistory(row) {
    selected.value = row
    historyOpen.value = true
  }
</script>

<style scoped>
  .stock-card :deep(th) {
    font-weight: 700 !important;
    white-space: normal;
    line-height: 1.2;
    vertical-align: bottom;
  }
</style>
