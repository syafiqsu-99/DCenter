<template>
  <v-card border flat class="fill-card">
    <div class="d-flex flex-wrap ga-4 px-4 py-3 text-body-2">
      <span>Normal <strong>{{ kg(store.balanceTotals.normalKg) }} kg</strong></span>
      <span>Baking <strong>{{ kg(store.balanceTotals.bakingKg) }} kg</strong></span>
      <span>Activated <strong>{{ kg(store.balanceTotals.activatedKg) }} kg</strong></span>
      <span>Total <strong>{{ kg(store.balanceTotals.totalKg) }} kg</strong></span>
      <span v-if="store.balanceTotals.lowCount" class="text-warning">
        <v-icon size="small">mdi-alert</v-icon> {{ store.balanceTotals.lowCount }} low stock
      </span>
    </div>
    <v-divider />
    <div ref="tableArea" class="fill">
      <v-data-table v-model:expanded="expanded" :headers="headers" :items="store.filteredBalances" item-value="itemId"
                    :loading="store.loadingBalances" show-expand :items-per-page="-1" hide-default-footer
                    class="consumable-table" density="comfortable" fixed-header :height="tableHeight"
                    no-data-text="No consumables match the filters.">
        <template #loading><v-skeleton-loader type="table-row@8" /></template>
        <template #item.diaSpec="{ item }">
          <strong>{{ item.diaSpec }}</strong>
          <v-chip v-if="!item.isActive" size="x-small" variant="tonal" class="ms-1">Inactive</v-chip>
          <div class="text-caption text-medium-emphasis">{{ item.category }}</div>
        </template>
        <template #item.normalKg="{ item }">{{ kg(item.normalKg) }}</template>
        <template #item.bakingKg="{ item }">{{ item.bakingKg ? kg(item.bakingKg) : '—' }}</template>
        <template #item.activatedKg="{ item }">
          <span :class="{ 'text-warning font-weight-bold': item.needsRefill }">{{ kg(item.activatedKg) }}</span>
        </template>
        <template #item.totalKg="{ item }">
          <span :class="item.isLow ? 'text-warning font-weight-bold' : 'font-weight-bold'">{{ kg(item.totalKg) }}</span>
        </template>
        <template #item.minStockKg="{ item }">{{ item.minStockKg > 0 ? kg(item.minStockKg) : '—' }}</template>
        <template #item.lastIssuedOn="{ item }">{{ fmtDate(item.lastIssuedOn) || '—' }}</template>
        <template #item.status="{ item }">
          <v-chip v-if="item.isLow" size="x-small" color="warning" variant="tonal" class="me-1">Low</v-chip>
          <v-chip v-if="item.needsRefill" size="x-small" color="info" variant="tonal">Refill</v-chip>
        </template>
        <template #item.actions="{ item }">
          <div class="d-flex justify-end ga-1">
            <v-btn v-if="item.category !== ELECTRODE" size="small" variant="text" color="deep-orange" :disabled="item.activatedKg <= 0"
                   @click="openFinish(item)">Finished</v-btn>
          </div>
        </template>
        <template #expanded-row="{ columns, item }">
          <tr>
            <td :colspan="columns.length" class="bg-grey-lighten-5 pa-2">
              <LotBalanceTable :item="item" :key="`${item.itemId}-${refreshKey}`" @changed="reload" />
            </td>
          </tr>
        </template>
      </v-data-table>
    </div>
  </v-card>

  <FinishDialog v-model="finishOpen" :item="selected" @saved="reload" />
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { ref } from 'vue'
  import { useFillHeight } from '@/composables/useFillHeight'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, ELECTRODE, fmtDate, kg } from '@/utils/consumables'
  import LotBalanceTable from '@/components/consumables/inventory/LotBalanceTable.vue'
  import FinishDialog from '@/components/consumables/shared/FinishDialog.vue'

  const tableArea = ref(null)
  const tableHeight = useFillHeight(tableArea)
  const store = useConsumableStore()
  const expanded = ref([])
  const selected = ref(null)
  const finishOpen = ref(false)
  const refreshKey = ref(0)

  const headers = [
    { title: 'Consumable', key: 'diaSpec', width: '20%' },
    { title: COLUMN.normal, key: 'normalKg', align: 'end', width: '9%' },
    { title: COLUMN.baking, key: 'bakingKg', align: 'end', width: '8%' },
    { title: COLUMN.activated, key: 'activatedKg', align: 'end', width: '9%' },
    { title: COLUMN.total, key: 'totalKg', align: 'end', width: '9%' },
    { title: COLUMN.minStock, key: 'minStockKg', align: 'end', width: '8%' },
    { title: 'Lots', key: 'lotCount', align: 'end', width: '5%' },
    { title: 'Last pickup', key: 'lastIssuedOn', width: '9%' },
    { title: '', key: 'status', sortable: false, width: '8%' },
    { title: '', key: 'actions', sortable: false, align: 'end', width: '12%' },
    { title: '', key: 'data-table-expand', width: '4%' },
  ]

  function openFinish(row) {
    selected.value = row
    finishOpen.value = true
  }

  async function reload() {
    try {
      await store.loadBalances(true)
    } catch {
      store.stale()
    }
    refreshKey.value += 1
  }
</script>
