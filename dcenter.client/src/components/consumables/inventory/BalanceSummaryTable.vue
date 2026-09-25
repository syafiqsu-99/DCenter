<template>
  <v-card border flat class="fill-card">
    <div class="d-flex flex-wrap align-center ga-4 px-4 py-3 text-body-2">
      <span>Normal <strong>{{ kg(store.balanceTotals.normalKg) }} kg</strong></span>
      <span>Baking <strong>{{ kg(store.balanceTotals.bakingKg) }} kg</strong></span>
      <span>Activated <strong>{{ kg(store.balanceTotals.activatedKg) }} kg</strong></span>
      <span>Total <strong>{{ kg(store.balanceTotals.totalKg) }} kg</strong></span>
      <span v-if="store.balanceTotals.lowCount" class="text-warning">
        <v-icon size="small">mdi-alert</v-icon> {{ store.balanceTotals.lowCount }} low stock
      </span>
      <v-spacer />
      <span v-if="electrodeNote" class="text-caption text-medium-emphasis">
        Electrodes: send to baking here, then place and manage them in
        <router-link :to="{ name: 'consumable-baking', query: { view: 'ovens' } }" class="text-primary">Baking &amp; Holding</router-link>.
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
        <template #item.activatedMinKg="{ item }">{{ item.activatedMinKg > 0 ? kg(item.activatedMinKg) : '—' }}</template>
        <template #item.status="{ item }">
          <v-chip v-if="item.isLow" size="x-small" color="warning" variant="tonal" class="me-1">Low</v-chip>
          <v-chip v-if="item.needsRefill" size="x-small" color="info" variant="tonal">Refill</v-chip>
        </template>
        <template #item.actions="{ item }">
          <div class="d-flex justify-end flex-wrap ga-1">
            <v-btn v-if="blocked(item)" size="small" color="purple" variant="tonal" prepend-icon="mdi-fire"
                   :disabled="item.normalKg <= 0" @click="openBake(item)">Send to baking</v-btn>
            <template v-else>
              <v-btn size="small" color="primary" variant="tonal" prepend-icon="mdi-arrow-right"
                     :disabled="item.normalKg <= 0" @click="openTransfer(item, 'in')">Activate</v-btn>
              <v-btn size="small" variant="text" prepend-icon="mdi-arrow-left"
                     :disabled="item.activatedKg <= 0" @click="openTransfer(item, 'out')">Back to store</v-btn>
            </template>
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

  <TransferDialog v-model="transferOpen" :item="selected" :direction="direction" @saved="onSaved" />
  <FinishDialog v-model="finishOpen" :item="selected" @saved="onSaved" />
  <SendToBakeDialog v-model="bakeOpen" :item="selected" @saved="onBaked" />
  <v-snackbar v-model="snackbar" color="success" timeout="4000">{{ snackbarText }}</v-snackbar>
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { computed, ref } from 'vue'
  import { useFillHeight } from '@/composables/useFillHeight'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, ELECTRODE, kg } from '@/utils/consumables'
  import LotBalanceTable from '@/components/consumables/inventory/LotBalanceTable.vue'
  import TransferDialog from '@/components/consumables/transfers/TransferDialog.vue'
  import FinishDialog from '@/components/consumables/shared/FinishDialog.vue'
  import SendToBakeDialog from '@/components/consumables/baking/SendToBakeDialog.vue'

  const tableArea = ref(null)
  const tableHeight = useFillHeight(tableArea)
  const store = useConsumableStore()
  const expanded = ref([])
  const selected = ref(null)
  const direction = ref('in')
  const transferOpen = ref(false)
  const finishOpen = ref(false)
  const bakeOpen = ref(false)
  const refreshKey = ref(0)
  const snackbar = ref(false)
  const snackbarText = ref('')

  const headers = [
    { title: 'Consumable', key: 'diaSpec', width: '18%' },
    { title: COLUMN.normal, key: 'normalKg', align: 'end', width: '8%' },
    { title: COLUMN.baking, key: 'bakingKg', align: 'end', width: '7%' },
    { title: COLUMN.activated, key: 'activatedKg', align: 'end', width: '8%' },
    { title: COLUMN.total, key: 'totalKg', align: 'end', width: '8%' },
    { title: COLUMN.minStock, key: 'minStockKg', align: 'end', width: '7%' },
    { title: 'Activated Min', key: 'activatedMinKg', align: 'end', width: '8%' },
    { title: '', key: 'status', sortable: false, width: '7%' },
    { title: '', key: 'actions', sortable: false, align: 'end', width: '26%' },
    { title: '', key: 'data-table-expand', width: '3%' },
  ]

  const electrodeNote = computed(() =>
    !store.catalog.allowElectrodeDirectTransfer && store.filteredBalances.some((r) => r.category === ELECTRODE))

  const blocked = (row) => row.category === ELECTRODE && !store.catalog.allowElectrodeDirectTransfer

  function openTransfer(row, dir) {
    selected.value = row
    direction.value = dir
    transferOpen.value = true
  }

  function openFinish(row) {
    selected.value = row
    finishOpen.value = true
  }

  function openBake(row) {
    selected.value = row
    bakeOpen.value = true
  }

  async function reload() {
    try {
      await store.loadBalances(true)
    } catch {
      store.stale()
    }
    refreshKey.value += 1
  }

  async function onSaved(result) {
    if (result?.balance) {
      snackbarText.value = `${result.txnNo} saved — Normal ${kg(result.balance.normalKg)} kg · Activated ${kg(result.balance.activatedKg)} kg`
      snackbar.value = true
    }
    await reload()
  }

  async function onBaked(result) {
    snackbarText.value = `${result.txnNo} saved — ${result.records.map((r) => r.bakingNo).join(', ')} queued for baking`
    snackbar.value = true
    await reload()
  }
</script>
