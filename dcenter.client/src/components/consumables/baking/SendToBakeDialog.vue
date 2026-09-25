<template>
  <v-dialog :model-value="modelValue" max-width="620" persistent @update:model-value="close">
    <v-card prepend-icon="mdi-fire" title="Send to baking" subtitle="Moves electrodes from Normal storage to Baking">
      <v-divider />
      <v-card-text>
        <v-row dense>
          <v-col cols="12">
            <ItemPicker v-if="!item" v-model="picked" :category="ELECTRODE" autofocus />
            <div v-else>
              <div class="text-subtitle-1 font-weight-bold">{{ item.diaSpec }}</div>
              <div class="text-caption text-medium-emphasis">Normal {{ kg(item.normalKg) }} kg</div>
            </div>
          </v-col>
          <v-col cols="12">
            <v-select v-model="lotId" :items="lotOptions" item-title="title" item-value="value" label="Lot" v-bind="field"
                      :loading="loadingLots" :disabled="!itemId" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model.number="qty" type="number" min="0.01" step="0.01" label="Quantity" suffix="kg" v-bind="field"
                          :hint="`Available ${kg(available)} kg`" persistent-hint />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model="bakingDate" type="date" label="Baking Date" :max="todayIso()" v-bind="field" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-combobox v-model="pic" :items="picOptions" label="Person In Charge" v-bind="field" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model="remarks" label="Remarks" v-bind="field" />
          </v-col>
          <v-col cols="12" class="d-flex flex-wrap ga-1">
            <v-chip v-for="q in QUICK_QTY" :key="q" size="small" variant="tonal" @click="qty = q">{{ kg(q) }}</v-chip>
            <v-chip size="small" variant="tonal" :disabled="available <= 0" @click="qty = available">All ({{ kg(available) }})</v-chip>
          </v-col>
        </v-row>
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
        <v-btn color="purple" variant="flat" :loading="saving" :disabled="!canSave" @click="submit">Send to baking</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { computed, ref, watch } from 'vue'
  import { storeToRefs } from 'pinia'
  import { useConsumableStore } from '@/store/consumableStore'
  import { useLookupStore } from '@/store/lookupStore'
  import { ELECTRODE, QUICK_QTY, errorText, kg, todayIso } from '@/utils/consumables'
  import ItemPicker from '@/components/consumables/shared/ItemPicker.vue'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    item: { type: Object, default: null },
  })
  const emit = defineEmits(['update:modelValue', 'saved'])

  const store = useConsumableStore()
  const lookupStore = useLookupStore()
  const { options } = storeToRefs(lookupStore)
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const picked = ref(null)
  const lots = ref([])
  const loadingLots = ref(false)
  const lotId = ref(null)
  const qty = ref(null)
  const bakingDate = ref(todayIso())
  const pic = ref('')
  const remarks = ref('')
  const saving = ref(false)
  const error = ref('')

  const itemId = computed(() => props.item?.itemId ?? picked.value?.id ?? null)
  const picOptions = computed(() => options.value?.ConsumablePIC ?? [])
  const normalLots = computed(() => lots.value.filter((l) => l.normalKg > 0))
  const lotOptions = computed(() =>
    normalLots.value.map((l) => ({ title: `${l.lotNumber} · ${l.brand} · ${kg(l.normalKg)} kg`, value: l.lotId })))
  const available = computed(() => normalLots.value.find((l) => l.lotId === lotId.value)?.normalKg ?? 0)
  const canSave = computed(() =>
    !!itemId.value && lotId.value !== null && Number(qty.value) > 0 && Number(qty.value) <= available.value && !!(pic.value ?? '').trim() && store.hasEnteredBy)

  async function loadLots(id) {
    lots.value = []
    if (!id) return
    loadingLots.value = true
    try {
      lots.value = [...await store.lotBalances(id)].sort((a, b) => a.lotId - b.lotId)
      lotId.value = normalLots.value[0]?.lotId ?? null
    } catch (e) {
      error.value = errorText(e, 'Could not load the lots.')
    } finally {
      loadingLots.value = false
    }
  }

  watch(() => props.modelValue, (open) => {
    if (!open) return
    lookupStore.load().catch(() => {})
    picked.value = null
    lotId.value = null
    qty.value = null
    bakingDate.value = todayIso()
    pic.value = store.isSupervisor ? store.personInCharge : store.counterWelder?.welderName ?? store.personInCharge
    remarks.value = ''
    error.value = ''
    loadLots(props.item?.itemId)
  })

  watch(picked, (value) => {
    lotId.value = null
    loadLots(value?.id)
  })

  function close(value) {
    emit('update:modelValue', value)
  }

  async function submit() {
    if (!canSave.value || saving.value) return
    saving.value = true
    error.value = ''
    try {
      const result = await store.sendToBake({
        bakingDate: bakingDate.value,
        itemId: itemId.value,
        lotId: lotId.value,
        quantityKg: Number(qty.value),
        personInCharge: pic.value.trim(),
        remarks: remarks.value.trim() || null,
      })
      emit('saved', result)
      close(false)
    } catch (e) {
      error.value = errorText(e, 'Could not send to baking.')
    } finally {
      saving.value = false
    }
  }
</script>
