<template>
  <v-card border flat>
    <v-card-title class="d-flex flex-wrap align-center ga-3">
      <span>Normal ⇄ Activated storage</span>
      <v-spacer />
      <v-text-field v-model="filters.search" prepend-inner-icon="mdi-magnify" label="Search" clearable
                    variant="outlined" density="compact" hide-details style="max-width:240px;" />
      <v-btn-toggle v-model="filters.category" mandatory divided density="compact" variant="outlined" color="primary">
        <v-btn :value="ALL" size="small">All</v-btn>
        <v-btn v-for="c in store.catalog.categories" :key="c" :value="c" size="small">{{ c }}</v-btn>
      </v-btn-toggle>
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="store.loadingBalances" @click="load(true)">Refresh</v-btn>
    </v-card-title>

    <v-alert v-if="electrodeNote" type="info" variant="tonal" density="compact" class="mx-4 mb-2">
      Electrodes go through Baking and the holding ovens: use Send to baking here, and manage their Activated stock on the Holding Ovens tab.
    </v-alert>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mx-4 mb-2">{{ error }}</v-alert>

    <v-data-table-virtual :headers="headers" :items="store.transferRows" :loading="store.loadingBalances" item-value="itemId"
                          class="consumable-table" density="comfortable" fixed-header height="calc(100vh - 330px)"
                          no-data-text="Nothing in Normal or Activated storage.">
      <template #loading><v-skeleton-loader type="table-row@8" /></template>
      <template #item.diaSpec="{ item }">
        <strong>{{ item.diaSpec }}</strong>
        <div class="text-caption text-medium-emphasis">{{ item.category }}</div>
      </template>
      <template #item.normalKg="{ item }">{{ kg(item.normalKg) }}</template>
      <template #item.activatedKg="{ item }">
        <span :class="{ 'text-warning font-weight-bold': item.needsRefill }">{{ kg(item.activatedKg) }}</span>
        <v-chip v-if="item.needsRefill" size="x-small" color="warning" variant="tonal" class="ms-1">Refill</v-chip>
      </template>
      <template #item.activatedMinKg="{ item }">{{ item.activatedMinKg > 0 ? kg(item.activatedMinKg) : '—' }}</template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end flex-wrap ga-1">
          <v-btn v-if="blocked(item)" size="small" color="purple" variant="tonal" prepend-icon="mdi-fire"
                 :disabled="item.normalKg <= 0" @click="openBake(item)">Send to baking</v-btn>
          <template v-else>
            <v-btn size="small" color="primary" variant="tonal" prepend-icon="mdi-arrow-right"
                   :disabled="item.normalKg <= 0" @click="open(item, 'in')">Activate</v-btn>
            <v-btn size="small" variant="text" prepend-icon="mdi-arrow-left"
                   :disabled="item.activatedKg <= 0" @click="open(item, 'out')">Back to store</v-btn>
          </template>
          <v-btn v-if="item.category !== ELECTRODE" size="small" variant="text" color="deep-orange" :disabled="item.activatedKg <= 0"
                 @click="openFinish(item)">Finished</v-btn>
          <v-btn v-if="item.category !== ELECTRODE" size="small" variant="text" color="warning" @click="openAdjust(item)">Adjust</v-btn>
        </div>
      </template>
    </v-data-table-virtual>
  </v-card>

  <TransferDialog v-model="transferOpen" :item="selected" :direction="direction" @saved="onSaved" />
  <FinishDialog v-model="finishOpen" :item="selected" @saved="onSaved" />
  <AdjustDialog v-model="adjustOpen" :item="selected" @saved="onSaved" />
  <SendToBakeDialog v-model="bakeOpen" :item="selected" @saved="onBaked" />
  <v-snackbar v-model="snackbar" color="success" timeout="4000">{{ snackbarText }}</v-snackbar>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { ALL, ELECTRODE, errorText, kg } from '@/utils/consumables'
  import TransferDialog from '@/components/consumables/TransferDialog.vue'
  import FinishDialog from '@/components/consumables/FinishDialog.vue'
  import AdjustDialog from '@/components/consumables/AdjustDialog.vue'
  import SendToBakeDialog from '@/components/consumables/SendToBakeDialog.vue'

  const store = useConsumableStore()
  const filters = store.transferFilters
  const error = ref('')
  const selected = ref(null)
  const direction = ref('in')
  const transferOpen = ref(false)
  const finishOpen = ref(false)
  const adjustOpen = ref(false)
  const bakeOpen = ref(false)
  const snackbar = ref(false)
  const snackbarText = ref('')

  const headers = [
    { title: 'Consumable', key: 'diaSpec', width: '26%' },
    { title: 'Normal (KG)', key: 'normalKg', align: 'end', width: '12%' },
    { title: 'Activated (KG)', key: 'activatedKg', align: 'end', width: '15%' },
    { title: 'Activated Min', key: 'activatedMinKg', align: 'end', width: '11%' },
    { title: '', key: 'actions', sortable: false, align: 'end', width: '36%' },
  ]

  const electrodeNote = computed(() =>
    !store.catalog.allowElectrodeDirectTransfer && store.transferRows.some((r) => r.category === ELECTRODE))

  function blocked(row) {
    return row.category === ELECTRODE && !store.catalog.allowElectrodeDirectTransfer
  }

  async function load(force = false) {
    error.value = ''
    try {
      await store.loadBalances(force)
    } catch (e) {
      error.value = errorText(e, 'Could not load stock balances.')
    }
  }

  function open(row, dir) {
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

  async function onBaked(result) {
    snackbarText.value = `${result.txnNo} saved — ${result.records.map((r) => r.bakingNo).join(', ')} queued for baking`
    snackbar.value = true
    await load(true)
  }

  function openAdjust(row) {
    selected.value = row
    adjustOpen.value = true
  }

  async function onSaved(result) {
    snackbarText.value = `${result.txnNo} saved — Normal ${kg(result.balance.normalKg)} kg · Activated ${kg(result.balance.activatedKg)} kg`
    snackbar.value = true
    await load(true)
  }

  onMounted(() => load())
</script>
