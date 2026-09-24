<template>
  <v-dialog :model-value="modelValue" max-width="620" persistent @update:model-value="close">
    <v-card v-if="item" prepend-icon="mdi-scale-balance" title="Adjust to counted quantity"
            :subtitle="bin ? `${item.diaSpec} · ${bin.code}` : item.diaSpec">
      <v-divider />
      <v-card-text>
        <v-row dense>
          <v-col cols="12">
            <v-alert type="info" variant="tonal" density="compact">
              Adjusts Activated storage{{ bin ? ` in ${bin.code}` : '' }}. Normal storage is corrected through a Stock Count.
            </v-alert>
          </v-col>
          <v-col cols="12">
            <v-select v-model="lotId" :items="lotOptions" item-title="title" item-value="value" label="Lot"
                      v-bind="field" :loading="loadingLots" />
          </v-col>
          <v-col cols="12" sm="4">
            <v-text-field :model-value="kg(systemKg)" label="System (kg)" v-bind="field" readonly />
          </v-col>
          <v-col cols="12" sm="4">
            <v-text-field v-model.number="counted" type="number" min="0" step="0.01" label="Counted (kg)" suffix="kg"
                          v-bind="field" autofocus />
          </v-col>
          <v-col cols="12" sm="4">
            <v-text-field :model-value="differenceText" label="Difference" v-bind="field" readonly
                          :class="difference < 0 ? 'text-error' : difference > 0 ? 'text-success' : ''" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-select v-model="reason" :items="store.catalog.adjustReasons" label="Reason" v-bind="field" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model="remarks" label="Remarks" v-bind="field"
                          :error-messages="needsRemarks ? ['Describe the reason'] : []" />
          </v-col>
        </v-row>
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
        <v-btn color="warning" variant="flat" :loading="saving" :disabled="!canSave" @click="submit">Post adjustment</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { computed, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, kg, todayIso } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    item: { type: Object, default: null },
    lot: { type: Object, default: null },
    bin: { type: Object, default: null },
    binLots: { type: Array, default: null },
  })
  const emit = defineEmits(['update:modelValue', 'saved'])

  const store = useConsumableStore()
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const stage = ref('Activated')
  const lotId = ref(null)
  const lots = ref([])
  const loadingLots = ref(false)
  const counted = ref(null)
  const reason = ref('Count variance')
  const remarks = ref('')
  const saving = ref(false)
  const error = ref('')

  const itemId = computed(() => props.item?.itemId ?? props.item?.id)
  const stageKey = computed(() => (stage.value === 'Normal' ? 'normalKg' : 'activatedKg'))


  const lotOptions = computed(() => [
    { title: 'All lots (item level)', value: null },
    ...lots.value.map((l) => ({ title: `${l.lotNumber} · ${l.brand} · ${kg(l[stageKey.value])} kg`, value: l.lotId })),
  ])

  const systemKg = computed(() => {
    if (lotId.value === null) return lots.value.reduce((sum, l) => sum + l[stageKey.value], 0)
    return lots.value.find((l) => l.lotId === lotId.value)?.[stageKey.value] ?? 0
  })

  const difference = computed(() => (typeof counted.value === 'number' ? Math.round((counted.value - systemKg.value) * 100) / 100 : 0))
  const differenceText = computed(() => (difference.value > 0 ? `+${kg(difference.value)}` : kg(difference.value)))
  const needsRemarks = computed(() => reason.value === 'Other' && !remarks.value.trim())
  const canSave = computed(() =>
    typeof counted.value === 'number' && counted.value >= 0 && difference.value !== 0 && !needsRemarks.value && store.hasEnteredBy)

  watch(() => props.modelValue, async (open) => {
    if (!open) return
    stage.value = 'Activated'
    lotId.value = props.lot?.lotId ?? null
    counted.value = null
    reason.value = 'Count variance'
    remarks.value = ''
    error.value = ''
    if (props.bin) {
      lots.value = props.binLots ?? []
      return
    }
    loadingLots.value = true
    try {
      lots.value = await store.lotBalances(itemId.value, true)
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
    saving.value = true
    error.value = ''
    try {
      const result = await store.adjust({
        txnDate: todayIso(),
        itemId: itemId.value,
        lotId: lotId.value,
        stage: stage.value,
        countedQtyKg: counted.value,
        reason: reason.value,
        remarks: remarks.value.trim() || null,
        compartmentId: stage.value === 'Activated' ? props.bin?.id ?? null : null,
      })
      emit('saved', result)
      close(false)
    } catch (e) {
      error.value = errorText(e, 'Could not post the adjustment.')
    } finally {
      saving.value = false
    }
  }
</script>
