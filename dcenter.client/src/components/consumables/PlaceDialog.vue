<template>
  <v-dialog :model-value="modelValue" max-width="680" persistent scrollable @update:model-value="close">
    <v-card v-if="record" prepend-icon="mdi-archive-arrow-down" :title="`Place ${record.bakingNo}`"
            :subtitle="`${record.diaSpec} · ${record.brand} · Lot ${record.lotNumber} · ${kg(record.balanceKg)} kg baked`">
      <v-divider />
      <v-card-text>
        <v-btn-toggle v-model="finished" mandatory divided density="comfortable" variant="outlined" color="primary" class="mb-4 w-100">
          <v-btn :value="false" class="flex-grow-1" prepend-icon="mdi-view-grid-outline">Place in holding oven</v-btn>
          <v-btn :value="true" class="flex-grow-1" prepend-icon="mdi-account-hard-hat">Finished After Baking</v-btn>
        </v-btn-toggle>

        <v-row dense>
          <v-col cols="12" sm="6">
            <v-text-field v-model="holdingDate" type="date" label="Date" :max="todayIso()" v-bind="field" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model.number="qty" type="number" min="0.01" step="0.01" label="Quantity" suffix="kg" v-bind="field"
                          :disabled="takeAll" :hint="`Waiting ${kg(record.balanceKg)} kg`" persistent-hint />
          </v-col>
          <v-col cols="12" class="d-flex flex-wrap ga-1">
            <v-chip size="small" :color="takeAll ? 'primary' : undefined" :variant="takeAll ? 'flat' : 'tonal'"
                    prepend-icon="mdi-select-all" @click="toggleAll">All ({{ kg(record.balanceKg) }})</v-chip>
            <v-chip v-for="q in QUICK_QTY" :key="q" size="small" variant="tonal" :disabled="takeAll" @click="qty = q">{{ kg(q) }}</v-chip>
          </v-col>
          <v-col cols="12">
            <WelderPicker v-model="welder" />
            <div class="text-caption text-medium-emphasis mt-1">
              {{ finished ? 'Required — the welder who takes the electrodes straight from baking.' : 'Optional — the welder this batch was prepared for.' }}
            </div>
          </v-col>
          <v-col v-if="!finished" cols="12">
            <div class="text-overline text-medium-emphasis">Compartment</div>
            <CompartmentPicker v-model="compartmentId" :oven-type="record.holdingOvenType" :item-id="record.itemId" />
          </v-col>
          <v-col cols="12"><v-text-field v-model="remarks" label="Remarks" v-bind="field" /></v-col>
        </v-row>
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
        <v-btn color="indigo" variant="flat" :loading="saving" :disabled="!canSave" @click="submit">
          {{ finished ? 'Issue to welder' : 'Place' }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { computed, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { QUICK_QTY, errorText, kg, todayIso } from '@/utils/consumables'
  import WelderPicker from '@/components/consumables/WelderPicker.vue'
  import CompartmentPicker from '@/components/consumables/CompartmentPicker.vue'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    record: { type: Object, default: null },
  })
  const emit = defineEmits(['update:modelValue', 'saved'])

  const store = useConsumableStore()
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const finished = ref(false)
  const holdingDate = ref(todayIso())
  const qty = ref(null)
  const takeAll = ref(true)
  const welder = ref(null)
  const compartmentId = ref(null)
  const remarks = ref('')
  const saving = ref(false)
  const error = ref('')

  const canSave = computed(() => {
    if (!store.hasEnteredBy || !props.record) return false
    if (!takeAll.value && !(Number(qty.value) > 0 && Number(qty.value) <= props.record.balanceKg)) return false
    return finished.value ? !!welder.value : !!compartmentId.value
  })

  watch(() => props.modelValue, (open) => {
    if (!open) return
    finished.value = false
    holdingDate.value = todayIso()
    takeAll.value = true
    qty.value = props.record?.balanceKg ?? null
    welder.value = store.isSupervisor ? null : store.counterWelder
    compartmentId.value = null
    remarks.value = ''
    error.value = ''
    store.loadOvens(true).catch(() => {})
  })

  function toggleAll() {
    takeAll.value = !takeAll.value
    qty.value = takeAll.value ? props.record?.balanceKg ?? null : null
  }

  function close(value) {
    emit('update:modelValue', value)
  }

  async function submit() {
    if (!canSave.value || saving.value || !props.record) return
    saving.value = true
    error.value = ''
    try {
      const result = await store.place({
        holdingDate: holdingDate.value,
        bakingRecordId: props.record.id,
        compartmentId: finished.value ? null : compartmentId.value,
        finishedAfterBaking: finished.value,
        welderId: welder.value?.id ?? null,
        quantityKg: takeAll.value ? 0 : Number(qty.value),
        takeAll: takeAll.value,
        remarks: remarks.value.trim() || null,
      })
      emit('saved', result)
      close(false)
    } catch (e) {
      error.value = errorText(e, 'Could not save the holding record.')
    } finally {
      saving.value = false
    }
  }
</script>
