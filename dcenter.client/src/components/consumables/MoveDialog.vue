<template>
  <v-dialog :model-value="modelValue" max-width="600" persistent scrollable @update:model-value="close">
    <v-card v-if="lot" prepend-icon="mdi-swap-horizontal" :title="`Move from ${from?.code ?? ''}`"
            :subtitle="`${lot.diaSpec} · ${lot.brand} · Lot ${lot.lotNumber} · ${kg(lot.kg)} kg`">
      <v-divider />
      <v-card-text>
        <v-row dense>
          <v-col cols="12" sm="6">
            <v-text-field v-model.number="qty" type="number" min="0.01" step="0.01" label="Quantity" suffix="kg" v-bind="field"
                          :disabled="takeAll" />
          </v-col>
          <v-col cols="12" sm="6" class="d-flex align-center">
            <v-chip :color="takeAll ? 'primary' : undefined" :variant="takeAll ? 'flat' : 'tonal'" prepend-icon="mdi-select-all"
                    @click="toggleAll">All ({{ kg(lot.kg) }})</v-chip>
          </v-col>
          <v-col cols="12">
            <div class="text-overline text-medium-emphasis">Move to</div>
            <CompartmentPicker v-model="toId" :oven-type="lot.holdingOvenType" :item-id="lot.itemId" :exclude-id="from?.id ?? null" />
          </v-col>
          <v-col cols="12"><v-text-field v-model="remarks" label="Remarks" v-bind="field" /></v-col>
        </v-row>
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" :loading="saving" :disabled="!canSave" @click="submit">Move</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { computed, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, kg, todayIso } from '@/utils/consumables'
  import CompartmentPicker from '@/components/consumables/CompartmentPicker.vue'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    lot: { type: Object, default: null },
    from: { type: Object, default: null },
  })
  const emit = defineEmits(['update:modelValue', 'saved'])

  const store = useConsumableStore()
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const qty = ref(null)
  const takeAll = ref(true)
  const toId = ref(null)
  const remarks = ref('')
  const saving = ref(false)
  const error = ref('')

  const canSave = computed(() =>
    store.hasEnteredBy && !!props.lot && !!props.from && !!toId.value
    && (takeAll.value || (Number(qty.value) > 0 && Number(qty.value) <= props.lot.kg)))

  watch(() => props.modelValue, (open) => {
    if (!open) return
    takeAll.value = true
    qty.value = props.lot?.kg ?? null
    toId.value = null
    remarks.value = ''
    error.value = ''
    store.loadOvens(true).catch(() => {})
  })

  function toggleAll() {
    takeAll.value = !takeAll.value
    qty.value = takeAll.value ? props.lot?.kg ?? null : null
  }

  function close(value) {
    emit('update:modelValue', value)
  }

  async function submit() {
    if (!canSave.value || saving.value) return
    saving.value = true
    error.value = ''
    try {
      const result = await store.move({
        txnDate: todayIso(),
        itemId: props.lot.itemId,
        lotId: props.lot.lotId,
        fromCompartmentId: props.from.id ?? null,
        toCompartmentId: toId.value,
        quantityKg: takeAll.value ? 0 : Number(qty.value),
        takeAll: takeAll.value,
        remarks: remarks.value.trim() || null,
      })
      emit('saved', result)
      close(false)
    } catch (e) {
      error.value = errorText(e, 'Could not move the electrodes.')
    } finally {
      saving.value = false
    }
  }
</script>
