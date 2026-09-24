<template>
  <v-dialog :model-value="modelValue" max-width="640" persistent @update:model-value="close">
    <v-card v-if="record" prepend-icon="mdi-pencil" :title="`Edit ${record.bakingNo}`"
            :subtitle="`${record.diaSpec} · ${record.brand} · Lot ${record.lotNumber}`">
      <v-divider />
      <v-card-text>
        <v-row dense>
          <v-col cols="12" sm="6">
            <v-combobox v-model="form.personInCharge" :items="picOptions" label="Person In Charge" v-bind="field" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model="form.bakingDate" type="date" label="Baking Date" :max="todayIso()" v-bind="field" />
          </v-col>
          <v-col cols="12" sm="6"><v-text-field v-model="form.bakeStart" type="datetime-local" label="Start Time" v-bind="field" /></v-col>
          <v-col cols="12" sm="6"><v-text-field v-model="form.bakeStop" type="datetime-local" label="Stop Time" v-bind="field" /></v-col>
          <v-col cols="12" sm="6"><v-text-field v-model="form.rebakeStart" type="datetime-local" label="Re-Baking Start" v-bind="field" /></v-col>
          <v-col cols="12" sm="6"><v-text-field v-model="form.rebakeStop" type="datetime-local" label="Re-Baking Stop" v-bind="field" /></v-col>
          <v-col cols="12"><v-text-field v-model="form.remarks" label="Remarks" v-bind="field" /></v-col>
        </v-row>
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" :loading="saving" :disabled="!store.hasEnteredBy" @click="submit">Save</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { computed, reactive, ref, watch } from 'vue'
  import { storeToRefs } from 'pinia'
  import { useConsumableStore } from '@/store/consumableStore'
  import { useLookupStore } from '@/store/lookupStore'
  import { errorText, fromLocalInput, toLocalInput, todayIso } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    record: { type: Object, default: null },
  })
  const emit = defineEmits(['update:modelValue', 'saved'])

  const store = useConsumableStore()
  const lookupStore = useLookupStore()
  const { options } = storeToRefs(lookupStore)
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const form = reactive({ personInCharge: '', bakingDate: '', bakeStart: '', bakeStop: '', rebakeStart: '', rebakeStop: '', remarks: '' })
  const saving = ref(false)
  const error = ref('')

  const picOptions = computed(() => options.value?.ConsumablePIC ?? [])

  watch(() => props.modelValue, (open) => {
    if (!open || !props.record) return
    lookupStore.load().catch(() => {})
    const r = props.record
    Object.assign(form, {
      personInCharge: r.personInCharge,
      bakingDate: r.bakingDate,
      bakeStart: toLocalInput(r.bakeStart),
      bakeStop: toLocalInput(r.bakeStop),
      rebakeStart: toLocalInput(r.rebakeStart),
      rebakeStop: toLocalInput(r.rebakeStop),
      remarks: r.remarks ?? '',
    })
    error.value = ''
  })

  function close(value) {
    emit('update:modelValue', value)
  }

  async function submit() {
    saving.value = true
    error.value = ''
    try {
      const result = await store.updateBaking(props.record.id, {
        personInCharge: (form.personInCharge ?? '').trim(),
        bakingDate: form.bakingDate,
        bakeStart: fromLocalInput(form.bakeStart),
        bakeStop: fromLocalInput(form.bakeStop),
        rebakeStart: fromLocalInput(form.rebakeStart),
        rebakeStop: fromLocalInput(form.rebakeStop),
        remarks: form.remarks.trim() || null,
      })
      emit('saved', result)
      close(false)
    } catch (e) {
      error.value = errorText(e, 'Could not save the baking record.')
    } finally {
      saving.value = false
    }
  }
</script>
