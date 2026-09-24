<template>
  <div class="fill-card" :class="{ 'baking-board--fixed': operator }">
    <div class="d-flex flex-wrap align-center ga-2 mb-3 flex-shrink-0">
      <v-btn :size="operator ? 'x-large' : 'default'" color="purple" variant="flat" prepend-icon="mdi-plus" @click="sendOpen = true">Send to baking</v-btn>
      <v-btn :size="operator ? 'x-large' : 'default'" variant="tonal" prepend-icon="mdi-play" :disabled="!startable.length" :loading="stamping === 'start'"
             @click="stamp('start', startable)">
        Start selected ({{ startable.length }})
      </v-btn>
      <v-btn :size="operator ? 'x-large' : 'default'" variant="tonal" prepend-icon="mdi-stop" :disabled="!stoppable.length" :loading="stamping === 'stop'"
             @click="stamp('stop', stoppable)">
        Stop selected ({{ stoppable.length }})
      </v-btn>
      <v-btn v-if="selected.length" variant="text" @click="selected = []">Clear selection</v-btn>
      <v-spacer />
      <span class="text-caption text-medium-emphasis">Updated {{ fmtTime(now.toISOString()) }}</span>
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="store.loadingBaking" @click="load">Refresh</v-btn>
    </div>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-3 flex-shrink-0" closable @click:close="error = ''">
      {{ error }}
    </v-alert>

    <v-row class="fill board-row">
      <v-col v-for="col in columns" :key="col.key" cols="12" md="4" class="board-col">
        <v-card border flat class="h-100 d-flex flex-column">
          <v-card-title class="text-subtitle-1 d-flex align-center">
            <v-checkbox-btn v-if="col.key !== 'baked' && col.records.length" :model-value="allSelected(col)"
                            :indeterminate="!allSelected(col) && someSelected(col)" density="compact" class="flex-grow-0 me-1"
                            :aria-label="`Select all ${col.title}`" @update:model-value="toggleColumn(col)" />
            <v-icon :color="col.color" class="me-2">{{ col.icon }}</v-icon>
            {{ col.title }}
            <v-chip size="x-small" class="ms-2" variant="tonal">{{ col.records.length }}</v-chip>
            <v-spacer />
            <span class="text-caption text-medium-emphasis">{{ kg(col.records.reduce((s, r) => s + r.balanceKg, 0)) }} kg</span>
          </v-card-title>
          <v-divider />
          <v-card-text class="d-flex flex-column ga-2 flex-grow-1 overflow-y-auto" style="min-height:0;">
            <div v-if="!col.records.length" class="text-medium-emphasis text-body-2 text-center py-6">{{ col.empty }}</div>
            <v-card v-for="r in col.records" :key="r.id" border flat class="flex-shrink-0"
                    :color="selected.includes(r.id) ? 'blue-grey-lighten-5' : undefined">
              <v-card-text class="pa-3">
                <div class="d-flex align-start">
                  <v-checkbox-btn v-if="col.key !== 'baked'" v-model="selected" :value="r.id" density="compact" class="flex-grow-0 me-1" />
                  <div class="flex-grow-1">
                    <div class="d-flex align-center flex-wrap ga-1">
                      <strong>{{ r.diaSpec }}</strong>
                      <v-chip size="x-small" :color="BAKING_COLORS[r.status]" variant="tonal" label>{{ BAKING_LABELS[r.status] }}</v-chip>
                    </div>
                    <div class="text-caption">{{ r.bakingNo }} · {{ r.brand }} · Lot {{ r.lotNumber }}</div>
                    <div class="text-caption text-medium-emphasis">
                      {{ kg(r.balanceKg) }} kg · PIC {{ r.personInCharge }}
                      <template v-if="r.holdingOvenType"> · {{ r.holdingOvenType }} oven</template>
                    </div>
                    <div class="text-caption text-medium-emphasis">{{ timing(r) }}</div>
                  </div>
                </div>
              </v-card-text>
              <v-card-actions class="pt-0 px-3 pb-2 flex-wrap">
                <v-btn v-if="canStart(r)" :size="operator ? 'large' : 'small'" variant="tonal" color="deep-orange" prepend-icon="mdi-play"
                       @click="stamp('start', [r.id])">Start now</v-btn>
                <v-btn v-if="canStop(r)" :size="operator ? 'large' : 'small'" variant="tonal" color="success" prepend-icon="mdi-stop"
                       @click="stamp('stop', [r.id])">Stop now</v-btn>
                <v-btn v-if="canPlace(r)" :size="operator ? 'large' : 'small'" variant="flat" color="indigo" prepend-icon="mdi-archive-arrow-down"
                       @click="openPlace(r)">Place</v-btn>
                <v-spacer />
                <v-btn v-if="!operator" size="small" variant="text" icon="mdi-pencil" :aria-label="`Edit ${r.bakingNo}`" @click="openEdit(r)" />
              </v-card-actions>
            </v-card>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>

  <SendToBakeDialog v-model="sendOpen" @saved="onSaved" />
  <BakingEditDialog v-model="editOpen" :record="current" @saved="onSaved" />
  <PlaceDialog v-model="placeOpen" :record="current" @saved="onSaved" />
  <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="snackbarColor === 'warning' ? 8000 : 4000">{{ snackbarText }}</v-snackbar>
</template>

<script setup>
  import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { BAKING_COLORS, BAKING_LABELS, elapsed, errorText, fmtDateTime, fmtTime, kg } from '@/utils/consumables'
  import SendToBakeDialog from '@/components/consumables/baking/SendToBakeDialog.vue'
  import BakingEditDialog from '@/components/consumables/baking/BakingEditDialog.vue'
  import PlaceDialog from '@/components/consumables/baking/PlaceDialog.vue'

  defineProps({ operator: { type: Boolean, default: false } })
  const emit = defineEmits(['changed'])

  const store = useConsumableStore()
  const error = ref('')
  const selected = ref([])
  const stamping = ref('')
  const sendOpen = ref(false)
  const editOpen = ref(false)
  const placeOpen = ref(false)
  const current = ref(null)
  const snackbar = ref(false)
  const snackbarText = ref('')
  const snackbarColor = ref('success')
  const now = ref(new Date())
  let timer = null

  const QUEUED = ['Queued', 'RebakeQueued']
  const BAKING = ['Baking', 'Rebaking']
  const BAKED = ['Baked', 'Rebaked']

  const columns = computed(() => [
    { key: 'queued', title: 'Queued', icon: 'mdi-timer-sand', color: 'grey', empty: 'Nothing waiting to bake.',
      records: store.bakingBoard.filter((r) => QUEUED.includes(r.status)) },
    { key: 'baking', title: 'In the oven', icon: 'mdi-fire', color: 'deep-orange', empty: 'Nothing baking right now.',
      records: store.bakingBoard.filter((r) => BAKING.includes(r.status)) },
    { key: 'baked', title: 'Baked — awaiting placement', icon: 'mdi-check-circle', color: 'success', empty: 'Nothing waiting to be placed.',
      records: store.bakingBoard.filter((r) => BAKED.includes(r.status)) },
  ])

  const byId = computed(() => new Map(store.bakingBoard.map((r) => [r.id, r])))
  const startable = computed(() => selected.value.filter((id) => QUEUED.includes(byId.value.get(id)?.status)))
  const stoppable = computed(() => selected.value.filter((id) => BAKING.includes(byId.value.get(id)?.status)))

  const allSelected = (col) => col.records.length > 0 && col.records.every((r) => selected.value.includes(r.id))
  const someSelected = (col) => col.records.some((r) => selected.value.includes(r.id))

  function toggleColumn(col) {
    const ids = col.records.map((r) => r.id)
    selected.value = allSelected(col)
      ? selected.value.filter((id) => !ids.includes(id))
      : [...new Set([...selected.value, ...ids])]
  }

  const canStart = (r) => QUEUED.includes(r.status)
  const canStop = (r) => BAKING.includes(r.status)
  const canPlace = (r) => BAKED.includes(r.status) && r.balanceKg > 0

  function timing(r) {
    if (r.status === 'Rebaking') return `Re-baking ${elapsed(r.rebakeStart, now.value.toISOString())} (since ${fmtDateTime(r.rebakeStart)})`
    if (r.status === 'Rebaked') return `Re-baked ${fmtDateTime(r.rebakeStart)} → ${fmtDateTime(r.rebakeStop)}`
    if (r.status === 'RebakeQueued') return `Returned for re-bake · first bake ${fmtDateTime(r.bakeStart)} → ${fmtDateTime(r.bakeStop)}`
    if (r.status === 'Baking') return `Baking ${elapsed(r.bakeStart, now.value.toISOString())} (since ${fmtDateTime(r.bakeStart)})`
    if (r.status === 'Baked') return `Baked ${fmtDateTime(r.bakeStart)} → ${fmtDateTime(r.bakeStop)} (${elapsed(r.bakeStart, r.bakeStop)})`
    return `Queued ${fmtDateTime(r.createdAt)}`
  }

  async function load() {
    error.value = ''
    try {
      await store.loadBakingBoard()
      now.value = new Date()
      selected.value = selected.value.filter((id) => byId.value.has(id))
    } catch (e) {
      error.value = errorText(e, 'Could not load the baking board.')
    }
  }

  async function stamp(action, ids) {
    stamping.value = action
    error.value = ''
    try {
      if (action === 'start') await store.startBaking(ids)
      else await store.stopBaking(ids)
      selected.value = selected.value.filter((id) => !ids.includes(id))
      await load()
    } catch (e) {
      error.value = errorText(e, action === 'start' ? 'Could not start baking.' : 'Could not stop baking.')
    } finally {
      stamping.value = ''
    }
  }

  function openEdit(r) {
    current.value = r
    editOpen.value = true
  }

  function openPlace(r) {
    current.value = r
    placeOpen.value = true
  }

  async function onSaved(result) {
    emit('changed')
    snackbarColor.value = result?.warning ? 'warning' : 'success'
    snackbarText.value = result?.warning ?? 'Saved.'
    snackbar.value = true
    await load()
  }

  onMounted(() => {
    load()
    timer = setInterval(() => { now.value = new Date() }, 60000)
  })
  onBeforeUnmount(() => clearInterval(timer))
</script>

<style scoped>
  .baking-board--fixed {
    height: 560px;
    flex: 0 0 auto;
  }
  @media (max-width: 959px) {
    .baking-board--fixed {
      height: auto;
    }
  }

  @media (min-width: 960px) {
    .board-row {
      overflow: visible;
    }

    .board-col {
      height: 100%;
    }
  }
</style>
