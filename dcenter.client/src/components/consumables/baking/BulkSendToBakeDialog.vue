<template>
  <v-dialog :model-value="modelValue" max-width="720" persistent scrollable @update:model-value="close">
    <v-card prepend-icon="mdi-fire" title="Send selected to baking"
            :subtitle="`${lines.length} electrode(s) · ${kg(totalKg)} kg · oldest lot first, one baking record per lot`">
      <v-divider />
      <v-card-text>
        <v-table density="compact" class="mb-4">
          <thead>
            <tr>
              <th>Electrode</th>
              <th class="text-end">In Normal (kg)</th>
              <th class="text-end" style="width:170px;">Qty to bake</th>
              <th style="width:48px;" />
            </tr>
          </thead>
          <tbody>
            <tr v-for="l in lines" :key="l.itemId" :class="{ 'bg-red-lighten-5': l.itemId === failedId }">
              <td>
                <strong>{{ l.diaSpec }}</strong>
                <div class="text-caption text-medium-emphasis">{{ l.holdingOvenType ?? 'Holding oven not set' }}</div>
              </td>
              <td class="text-end">{{ kg(l.normalKg) }}</td>
              <td>
                <v-text-field v-model.number="l.qty" type="number" min="0.01" step="0.01" :max="l.normalKg" suffix="kg"
                              variant="outlined" density="compact" hide-details :disabled="saving"
                              :error="!validQty(l)" :aria-label="`Quantity of ${l.diaSpec}`" />
              </td>
              <td>
                <v-btn icon="mdi-close" size="small" variant="text" :disabled="saving" :aria-label="`Remove ${l.diaSpec}`"
                       @click="remove(l.itemId)" />
              </td>
            </tr>
          </tbody>
        </v-table>

        <v-row dense>
          <v-col cols="12" sm="6">
            <v-combobox v-model="pic" :items="picOptions" label="Person In Charge" v-bind="field" :disabled="saving" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model="bakingDate" type="date" label="Baking Date" :max="todayIso()" v-bind="field" :disabled="saving" />
          </v-col>
        </v-row>

        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <span v-if="saving" class="text-body-2 text-medium-emphasis">Sending {{ progress }}…</span>
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
        <v-btn color="purple" variant="flat" prepend-icon="mdi-fire" :loading="saving" :disabled="!canSave" @click="submit">
          {{ error ? 'Retry' : `Send ${lines.length} to baking` }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { computed, ref, watch } from 'vue'
  import { storeToRefs } from 'pinia'
  import { useConsumableStore } from '@/store/consumableStore'
  import { useLookupStore } from '@/store/lookupStore'
  import { errorText, kg, todayIso } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    items: { type: Array, default: () => [] },
  })
  const emit = defineEmits(['update:modelValue', 'saved'])

  const store = useConsumableStore()
  const lookupStore = useLookupStore()
  const { options } = storeToRefs(lookupStore)
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const lines = ref([])
  const pic = ref('')
  const bakingDate = ref(todayIso())
  const saving = ref(false)
  const error = ref('')
  const failedId = ref(null)
  const progress = ref('')
  let sent = 0

  const picOptions = computed(() => options.value?.ConsumablePIC ?? [])
  const totalKg = computed(() => lines.value.reduce((s, l) => s + (Number(l.qty) || 0), 0))
  const validQty = (l) => Number(l.qty) > 0 && Number(l.qty) <= l.normalKg
  const canSave = computed(() =>
    store.hasEnteredBy && lines.value.length > 0 && lines.value.every(validQty) && !!(pic.value ?? '').trim() && !!bakingDate.value)

  watch(() => props.modelValue, (open) => {
    if (!open) return
    lines.value = props.items.map((r) => ({ ...r, qty: r.qty ?? r.normalKg }))
    pic.value = store.isSupervisor ? store.personInCharge : store.counterWelder?.welderName ?? store.personInCharge
    bakingDate.value = todayIso()
    error.value = ''
    failedId.value = null
    sent = 0
    lookupStore.load().catch(() => {})
  })

  function remove(itemId) {
    lines.value = lines.value.filter((l) => l.itemId !== itemId)
    if (!lines.value.length) close(false)
  }

  function close(value) {
    if (!value && sent > 0) emit('saved', { count: sent })
    emit('update:modelValue', value)
  }

  async function submit() {
    if (!canSave.value || saving.value) return
    saving.value = true
    error.value = ''
    failedId.value = null
    const total = lines.value.length
    try {
      while (lines.value.length) {
        const l = lines.value[0]
        progress.value = `${total - lines.value.length + 1} of ${total}`
        try {
          await store.sendToBake({
            bakingDate: bakingDate.value, itemId: l.itemId, lotId: null, quantityKg: Number(l.qty),
            personInCharge: pic.value.trim(), remarks: null,
          })
        } catch (e) {
          failedId.value = l.itemId
          error.value = `${l.diaSpec}: ${errorText(e, 'Could not send to baking.')}`
          return
        }
        sent += 1
        lines.value = lines.value.slice(1)
      }
      close(false)
    } finally {
      saving.value = false
    }
  }
</script>
