<template>
  <v-navigation-drawer :model-value="modelValue" location="right" temporary width="440"
                       @update:model-value="emit('update:modelValue', $event)">
    <template v-if="compartment">
      <div class="pa-4">
        <div class="text-overline text-medium-emphasis">{{ oven?.name }}</div>
        <div class="text-h6">{{ compartment.code }}</div>
        <div class="text-body-2">
          {{ compartment.contents.length ? `${kg(compartment.totalKg)} kg · ${compartment.contents.length} lot(s)` : 'Empty' }}
        </div>
      </div>
      <v-divider />
      <div v-if="!compartment.contents.length" class="pa-4 text-medium-emphasis text-body-2">This compartment is empty.</div>
      <v-list v-else lines="three" density="compact">
        <v-list-item v-for="lot in compartment.contents" :key="lot.lotId">
          <v-list-item-title class="font-weight-bold">{{ lot.diaSpec }}</v-list-item-title>
          <v-list-item-subtitle>
            {{ lot.brand }} · Lot {{ lot.lotNumber }}<br>
            {{ kg(lot.kg) }} kg<template v-if="lot.sinceAt"> · held {{ elapsed(lot.sinceAt) }}</template>
          </v-list-item-subtitle>
          <div v-if="!readonly" class="d-flex flex-wrap ga-1 mt-2">
            <v-btn size="small" variant="tonal" prepend-icon="mdi-swap-horizontal" @click="openMove(lot)">Move</v-btn>
            <v-btn size="small" variant="tonal" color="deep-orange" @click="openFinish(lot)">Finished</v-btn>
            <v-btn size="small" variant="tonal" color="warning" @click="openAdjust(lot)">Adjust</v-btn>
            <v-btn size="small" variant="text" icon="mdi-history" :aria-label="`History of lot ${lot.lotNumber}`"
                   @click="openHistory(lot)" />
          </div>
        </v-list-item>
      </v-list>
      <v-divider />
      <div v-if="!readonly" class="pa-4 d-flex flex-column ga-2">
        <v-btn variant="text" prepend-icon="mdi-format-list-bulleted" @click="goHistory">All movements for {{ compartment.code }}</v-btn>
        <v-alert type="info" variant="tonal" density="compact">Welder pickups and returns are recorded in the Welder View.</v-alert>
      </div>
    </template>
  </v-navigation-drawer>

  <MoveDialog v-model="moveOpen" :lot="selected" :from="bin" @saved="changed" />
  <FinishDialog v-model="finishOpen" :item="finishItem" :lot="finishLot" :bin="bin" @saved="changed" />
  <AdjustDialog v-model="adjustOpen" :item="finishItem" :lot="finishLot" :bin="bin" :bin-lots="adjustLots" @saved="changed" />
  <LotHistoryDialog v-model="historyOpen" :lot="historyLot" />
</template>

<script setup>
  import { computed, ref } from 'vue'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { elapsed, kg } from '@/utils/consumables'
  import MoveDialog from '@/components/consumables/MoveDialog.vue'
  import FinishDialog from '@/components/consumables/FinishDialog.vue'
  import AdjustDialog from '@/components/consumables/AdjustDialog.vue'
  import LotHistoryDialog from '@/components/consumables/LotHistoryDialog.vue'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    oven: { type: Object, default: null },
    compartment: { type: Object, default: null },
    readonly: { type: Boolean, default: false },
  })
  const emit = defineEmits(['update:modelValue', 'changed'])

  const store = useConsumableStore()
  const router = useRouter()
  const selected = ref(null)
  const moveOpen = ref(false)
  const finishOpen = ref(false)
  const adjustOpen = ref(false)
  const historyOpen = ref(false)
  const historyLot = ref(null)

  const bin = computed(() => (props.compartment ? { id: props.compartment.id, code: props.compartment.code } : null))
  const finishItem = computed(() => {
    const lot = selected.value
    if (!lot) return null
    const kgInBin = props.compartment.contents.filter((x) => x.itemId === lot.itemId).reduce((s, x) => s + x.kg, 0)
    return { itemId: lot.itemId, diaSpec: lot.diaSpec, activatedKg: kgInBin }
  })
  const finishLot = computed(() =>
    selected.value ? { lotId: selected.value.lotId, lotNumber: selected.value.lotNumber, activatedKg: selected.value.kg } : null)
  const adjustLots = computed(() =>
    (props.compartment?.contents ?? [])
      .filter((x) => x.itemId === selected.value?.itemId)
      .map((x) => ({ lotId: x.lotId, brand: x.brand, lotNumber: x.lotNumber, activatedKg: x.kg, normalKg: 0 })))

  function openMove(lot) {
    selected.value = lot
    moveOpen.value = true
  }

  function openFinish(lot) {
    selected.value = lot
    finishOpen.value = true
  }

  function openAdjust(lot) {
    selected.value = lot
    adjustOpen.value = true
  }

  function openHistory(lot) {
    historyLot.value = lot
    historyOpen.value = true
  }

  function changed() {
    store.stale()
    emit('changed')
  }

  function goHistory() {
    store.showHistory({ from: '', to: '', compartmentId: props.compartment.id, compartmentCode: props.compartment.code })
    emit('update:modelValue', false)
    router.push({ name: 'consumable-history' })
  }
</script>
