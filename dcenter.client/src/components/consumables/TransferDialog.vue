<template>
  <v-dialog :model-value="modelValue" max-width="560" persistent @update:model-value="close">
    <v-card v-if="item" :prepend-icon="isIn ? 'mdi-arrow-right-bold' : 'mdi-arrow-left-bold'"
            :title="isIn ? 'Normal → Activated' : 'Activated → Normal'" :subtitle="item.diaSpec">
      <v-divider />
      <v-card-text>
        <v-row dense>
          <v-col cols="12">
            <v-select v-model="lotId" :items="lotOptions" item-title="title" item-value="value" label="Lot"
                      v-bind="field" :loading="loadingLots" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model.number="qty" type="number" min="0.01" step="0.01" label="Quantity" suffix="kg"
                          v-bind="field" autofocus :hint="`Available ${kg(available)} kg`" persistent-hint
                          @keydown.enter.prevent="submit" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model="remarks" label="Remarks" v-bind="field" />
          </v-col>
          <v-col cols="12" class="d-flex flex-wrap ga-1">
            <v-chip v-if="isIn && refillKg > 0" size="small" color="warning" variant="tonal" @click="qty = refillKg">
              Refill to min ({{ kg(refillKg) }})
            </v-chip>
            <v-chip v-for="q in QUICK_QTY" :key="q" size="small" variant="tonal" @click="qty = q">{{ kg(q) }}</v-chip>
            <v-chip size="small" variant="tonal" :disabled="available <= 0" @click="qty = available">All ({{ kg(available) }})</v-chip>
          </v-col>
        </v-row>
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" :loading="saving" :disabled="!canSave" @click="submit">Transfer</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { computed, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { QUICK_QTY, errorText, kg, todayIso } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    item: { type: Object, default: null },
    direction: { type: String, default: 'in' },
  })
  const emit = defineEmits(['update:modelValue', 'saved'])

  const store = useConsumableStore()
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const lots = ref([])
  const lotId = ref(null)
  const loadingLots = ref(false)
  const qty = ref(null)
  const remarks = ref('')
  const saving = ref(false)
  const error = ref('')

  const isIn = computed(() => props.direction === 'in')
  const fromKey = computed(() => (isIn.value ? 'normalKg' : 'activatedKg'))
  const sourceLots = computed(() => lots.value.filter((l) => l[fromKey.value] > 0))

  const lotOptions = computed(() => [
    { title: 'Auto — oldest lot first', value: null },
    ...sourceLots.value.map((l) => ({ title: `${l.lotNumber} · ${l.brand} · ${kg(l[fromKey.value])} kg`, value: l.lotId })),
  ])

  const available = computed(() =>
    lotId.value === null
      ? sourceLots.value.reduce((sum, l) => sum + l[fromKey.value], 0)
      : sourceLots.value.find((l) => l.lotId === lotId.value)?.[fromKey.value] ?? 0)

  const refillKg = computed(() => {
    if (!props.item) return 0
    const needed = props.item.activatedMinKg - props.item.activatedKg
    return needed > 0 ? Math.round(Math.min(needed, available.value) * 100) / 100 : 0
  })

  const canSave = computed(() => Number(qty.value) > 0 && Number(qty.value) <= available.value && store.hasEnteredBy)

  watch(() => props.modelValue, async (open) => {
    if (!open) return
    lotId.value = null
    qty.value = null
    remarks.value = ''
    error.value = ''
    loadingLots.value = true
    try {
      lots.value = await store.lotBalances(props.item.itemId)
      if (isIn.value && refillKg.value > 0) qty.value = refillKg.value
    } catch (e) {
      error.value = errorText(e, 'Could not load the lots.')
    } finally {
      loadingLots.value = false
    }
  })

  function close(value) {
    emit('update:modelValue', value)
  }

  async function submit() {
    if (!canSave.value || saving.value) return
    saving.value = true
    error.value = ''
    try {
      const result = await store.transfer({
        txnDate: todayIso(),
        itemId: props.item.itemId,
        lotId: lotId.value,
        fromStage: isIn.value ? 'Normal' : 'Activated',
        toStage: isIn.value ? 'Activated' : 'Normal',
        quantityKg: Number(qty.value),
        remarks: remarks.value.trim() || null,
      })
      emit('saved', result)
      close(false)
    } catch (e) {
      error.value = errorText(e, 'Could not save the transfer.')
    } finally {
      saving.value = false
    }
  }
</script>
