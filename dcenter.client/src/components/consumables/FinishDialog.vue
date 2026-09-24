<template>
  <v-dialog :model-value="modelValue" max-width="560" persistent @update:model-value="close">
    <v-card v-if="item" prepend-icon="mdi-flag-checkered" title="Mark as finished">
      <template #subtitle>
        {{ item.diaSpec }}<template v-if="lot"> · Lot {{ lot.lotNumber }}</template><template v-if="bin"> · {{ bin.code }}</template>
      </template>
      <v-divider />
      <v-card-text>
        <p class="text-body-2 mb-3">
          Posts the remaining <strong>{{ kg(remainingKg) }} kg</strong> in Activated storage as used up, so the system
          balance returns to zero when the rack is physically empty.
        </p>
        <v-row dense>
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
        <v-btn color="deep-orange" variant="flat" :loading="saving" :disabled="!canSave" @click="submit">
          Mark finished
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { computed, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, kg } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    item: { type: Object, default: null },
    lot: { type: Object, default: null },
    bin: { type: Object, default: null },
  })
  const emit = defineEmits(['update:modelValue', 'saved'])

  const store = useConsumableStore()
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const reason = ref('Used up')
  const remarks = ref('')
  const saving = ref(false)
  const error = ref('')

  const itemId = computed(() => props.item?.itemId ?? props.item?.id)
  const remainingKg = computed(() => props.lot?.activatedKg ?? props.item?.activatedKg ?? 0)
  const needsRemarks = computed(() => reason.value === 'Other' && !remarks.value.trim())
  const canSave = computed(() => remainingKg.value > 0 && !needsRemarks.value && store.hasEnteredBy)

  watch(() => props.modelValue, (open) => {
    if (!open) return
    reason.value = 'Used up'
    remarks.value = ''
    error.value = ''
  })

  function close(value) {
    emit('update:modelValue', value)
  }

  async function submit() {
    saving.value = true
    error.value = ''
    try {
      const result = await store.finish({
        itemId: itemId.value,
        lotId: props.lot?.lotId ?? null,
        stage: 'Activated',
        reason: reason.value,
        remarks: remarks.value.trim() || null,
        compartmentId: props.bin?.id ?? null,
      })
      emit('saved', result)
      close(false)
    } catch (e) {
      error.value = errorText(e, 'Could not mark this stock as finished.')
    } finally {
      saving.value = false
    }
  }
</script>
