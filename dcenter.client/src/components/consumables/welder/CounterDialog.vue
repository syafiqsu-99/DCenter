<template>
  <v-dialog :model-value="modelValue" max-width="640" persistent scrollable @update:model-value="close">
    <v-card :prepend-icon="isPickup ? 'mdi-tray-arrow-up' : 'mdi-keyboard-return'"
            :title="isPickup ? 'Pickup' : 'Return'" :subtitle="welder ? welder.welderName : ''">
      <v-divider />
      <v-card-text>
        <v-row dense>
          <v-col v-if="!item" cols="12">
            <ItemPicker v-model="picked" autofocus />
          </v-col>
          <v-col v-else cols="12">
            <div class="text-subtitle-1 font-weight-bold">{{ item.diaSpec }}</div>
            <div class="text-caption text-medium-emphasis">
              {{ item.category }} · Activated {{ kg(item.activatedKg) }} kg
            </div>
          </v-col>

          <v-col v-if="isPickup && isElectrode" cols="12">
            <div class="text-overline text-medium-emphasis">From compartment</div>
            <div class="d-flex flex-wrap ga-2">
              <v-chip v-for="b in item?.bins ?? []" :key="b.compartmentId ?? 'none'" label
                      :color="binKey === (b.compartmentId ?? 0) ? 'primary' : undefined"
                      :variant="binKey === (b.compartmentId ?? 0) ? 'flat' : 'tonal'" @click="selectBin(b)">
                {{ b.label }} · {{ kg(b.kg) }} kg
              </v-chip>
            </div>
          </v-col>

          <v-col v-if="!isPickup && isElectrode" cols="12">
            <v-btn-toggle v-model="returnTarget" mandatory divided density="comfortable" variant="outlined" color="teal" class="w-100">
              <v-btn value="compartment" class="flex-grow-1" prepend-icon="mdi-view-grid-outline">To compartment</v-btn>
              <v-btn value="rebake" class="flex-grow-1" prepend-icon="mdi-fire">Return for re-baking</v-btn>
            </v-btn-toggle>
          </v-col>

          <v-col cols="12">
            <v-select v-model="lotId" :items="lotOptions" item-title="title" item-value="value" label="Lot"
                      v-bind="field" :loading="loadingLots" :disabled="!itemId" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field ref="qtyField" v-model.number="qty" type="number" min="0.01" step="0.01"
                          :label="isPickup ? COLUMN.take : 'Return (KG)'" suffix="kg" v-bind="field"
                          :disabled="takeAll" :hint="isPickup ? `Available ${kg(available)} kg` : ''" persistent-hint
                          @keydown.enter.prevent="submit" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model="remarks" label="Remarks" v-bind="field" />
          </v-col>
          <v-col cols="12" class="d-flex flex-wrap ga-1">
            <v-chip v-for="q in QUICK_QTY" :key="q" size="small" variant="tonal" :disabled="takeAll" @click="qty = q">
              {{ kg(q) }}
            </v-chip>
            <v-chip v-if="isPickup" size="small" :color="takeAll ? 'info' : undefined" :variant="takeAll ? 'flat' : 'tonal'"
                    :disabled="available <= 0" prepend-icon="mdi-select-all" @click="toggleTakeAll">
              Take all of lot {{ selectedLotNumber }} ({{ kg(available) }})
            </v-chip>
          </v-col>

          <v-col v-if="!isPickup && isElectrode && returnTarget === 'compartment'" cols="12">
            <div class="text-overline text-medium-emphasis">Return to</div>
            <CompartmentPicker v-model="compartmentId" :oven-type="ovenType" :item-id="itemId" />
          </v-col>
          <v-col v-if="!isPickup && isElectrode && returnTarget === 'rebake'" cols="12">
            <v-alert type="info" variant="tonal" density="compact">
              The electrodes go back to Baking on the lot's latest baking record. Each record can be re-baked once.
            </v-alert>
          </v-col>
        </v-row>
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
        <v-btn :color="isPickup ? 'info' : 'teal'" variant="flat" :loading="saving" :disabled="!canSave" @click="submit">
          {{ isPickup ? 'Record pickup' : returnTarget === 'rebake' && isElectrode ? 'Return for re-baking' : 'Record return' }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { computed, nextTick, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, ELECTRODE, QUICK_QTY, errorText, kg, todayIso } from '@/utils/consumables'
  import ItemPicker from '@/components/consumables/shared/ItemPicker.vue'
  import CompartmentPicker from '@/components/consumables/holding/CompartmentPicker.vue'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    mode: { type: String, default: 'pickup' },
    item: { type: Object, default: null },
    welder: { type: Object, default: null },
  })
  const emit = defineEmits(['update:modelValue', 'saved'])

  const store = useConsumableStore()
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const picked = ref(null)
  const returnLots = ref([])
  const loadingLots = ref(false)
  const lotId = ref(null)
  const qty = ref(null)
  const takeAll = ref(false)
  const remarks = ref('')
  const binKey = ref(0)
  const returnTarget = ref('compartment')
  const compartmentId = ref(null)
  const saving = ref(false)
  const error = ref('')
  const qtyField = ref(null)

  const isPickup = computed(() => props.mode === 'pickup')
  const itemId = computed(() => props.item?.itemId ?? picked.value?.id ?? null)
  const isElectrode = computed(() => (props.item?.category ?? picked.value?.category) === ELECTRODE)
  const ovenType = computed(() => props.item?.holdingOvenType ?? picked.value?.holdingOvenType ?? null)
  const bin = computed(() => (props.item?.bins ?? []).find((b) => (b.compartmentId ?? 0) === binKey.value) ?? null)
  const pickupLots = computed(() => (isElectrode.value ? bin.value?.lots ?? [] : props.item?.lots ?? []))

  const lotOptions = computed(() => {
    if (isPickup.value) {
      return pickupLots.value.map((l) => ({ title: `${l.lotNumber} · ${l.brand} · ${kg(l.activatedKg)} kg`, value: l.lotId }))
    }
    return returnLots.value.map((l) => ({ title: `${l.lotNumber} · ${l.brand}`, value: l.id }))
  })

  const available = computed(() => {
    if (!isPickup.value || !props.item) return 0
    return pickupLots.value.find((l) => l.lotId === lotId.value)?.activatedKg ?? 0
  })

  const selectedLotNumber = computed(() => pickupLots.value.find((l) => l.lotId === lotId.value)?.lotNumber ?? '')

  watch(lotId, () => {
    if (takeAll.value) qty.value = available.value
  })

  const canSave = computed(() => {
    if (!props.welder || !itemId.value || !store.hasEnteredBy || lotId.value === null) return false
    if (isPickup.value && isElectrode.value && !bin.value) return false
    if (!isPickup.value && isElectrode.value && returnTarget.value === 'compartment' && !compartmentId.value) return false
    if (isPickup.value && takeAll.value) return available.value > 0
    const value = Number(qty.value)
    return value > 0 && (!isPickup.value || value <= available.value)
  })

  watch(() => props.modelValue, async (open) => {
    if (!open) return
    picked.value = null
    lotId.value = null
    qty.value = null
    takeAll.value = false
    remarks.value = ''
    error.value = ''
    returnLots.value = []
    returnTarget.value = 'compartment'
    compartmentId.value = props.item?.lastCompartmentId ?? null
    const bins = props.item?.bins ?? []
    const preferred = bins.find((b) => b.compartmentId === props.item?.lastCompartmentId) ?? bins[0]
    binKey.value = preferred ? preferred.compartmentId ?? 0 : 0
    if (isPickup.value) {
      lotId.value = firstLot()
    } else {
      store.loadOvens(true)
      if (props.item) {
        await loadReturnLots(props.item.itemId)
        lotId.value = returnLots.value.some((l) => l.id === props.item.lastLotId) ? props.item.lastLotId : returnLots.value[0]?.id ?? null
      }
    }
    await nextTick()
    qtyField.value?.focus()
  })

  watch(picked, async (value) => {
    lotId.value = null
    compartmentId.value = null
    returnLots.value = []
    if (value && !isPickup.value) {
      await loadReturnLots(value.id)
      lotId.value = returnLots.value[0]?.id ?? null
    }
  })

  function firstLot() {
    return pickupLots.value[0]?.lotId ?? null
  }

  async function loadReturnLots(id) {
    loadingLots.value = true
    try {
      returnLots.value = await store.lotsFor(id)
    } catch (e) {
      error.value = errorText(e, 'Could not load the lots.')
    } finally {
      loadingLots.value = false
    }
  }

  function selectBin(b) {
    binKey.value = b.compartmentId ?? 0
    lotId.value = firstLot()
    if (takeAll.value) qty.value = available.value
  }

  function toggleTakeAll() {
    takeAll.value = !takeAll.value
    qty.value = takeAll.value ? available.value : null
  }

  function close(value) {
    emit('update:modelValue', value)
  }

  async function submit() {
    if (!canSave.value || saving.value) return
    saving.value = true
    error.value = ''
    try {
      const base = {
        txnDate: todayIso(),
        welderId: props.welder.id,
        itemId: itemId.value,
        lotId: lotId.value,
        remarks: remarks.value.trim() || null,
      }
      let result
      if (isPickup.value) {
        result = await store.issue({
          ...base,
          quantityKg: takeAll.value ? 0 : Number(qty.value),
          takeAll: takeAll.value,
          compartmentId: isElectrode.value ? bin.value.compartmentId : null,
        })
      } else {
        const rebake = isElectrode.value && returnTarget.value === 'rebake'
        result = await store.returnStock({
          ...base,
          quantityKg: Number(qty.value),
          compartmentId: isElectrode.value && !rebake ? compartmentId.value : null,
          forRebake: rebake,
        })
      }
      emit('saved', result)
      close(false)
    } catch (e) {
      error.value = errorText(e, isPickup.value ? 'Could not record the pickup.' : 'Could not record the return.')
    } finally {
      saving.value = false
    }
  }
</script>
