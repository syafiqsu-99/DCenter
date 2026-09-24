<template>
  <v-dialog :model-value="modelValue" max-width="560" persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card prepend-icon="mdi-flag-checkered" title="Almost empty — is the rack finished?">
      <v-divider />
      <v-card-text>
        <p class="text-body-2 mb-2">
          Only a small amount is left in Activated storage. If the rack is physically empty, mark it finished so the
          balance matches reality.
        </p>
        <v-list density="compact">
          <v-list-item v-for="r in residuals" :key="r.lotId">
            <template #prepend>
              <v-checkbox-btn v-model="selected" :value="r.lotId" />
            </template>
            <v-list-item-title>{{ r.diaSpec }} · Lot {{ r.lotNumber }}<template v-if="r.compartmentLabel"> · {{ r.compartmentLabel }}</template></v-list-item-title>
            <v-list-item-subtitle>{{ r.brand }} · {{ kg(r.balanceKg) }} kg left</v-list-item-subtitle>
          </v-list-item>
        </v-list>
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="finish(false)">No, stock is still there</v-btn>
        <v-btn color="deep-orange" variant="flat" :loading="saving" :disabled="!selected.length" @click="finish(true)">
          Mark finished
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, kg } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    residuals: { type: Array, default: () => [] },
  })
  const emit = defineEmits(['update:modelValue', 'done'])

  const store = useConsumableStore()
  const selected = ref([])
  const saving = ref(false)
  const error = ref('')

  watch(() => props.modelValue, (open) => {
    if (!open) return
    selected.value = props.residuals.map((r) => r.lotId)
    error.value = ''
  })

  async function finish(confirm) {
    if (!confirm) {
      emit('update:modelValue', false)
      return
    }
    saving.value = true
    error.value = ''
    try {
      for (const r of props.residuals.filter((x) => selected.value.includes(x.lotId))) {
        await store.finish({
          itemId: r.itemId, lotId: r.lotId, stage: 'Activated', reason: 'Used up', remarks: null, compartmentId: r.compartmentId ?? null,
        })
      }
      emit('done')
      emit('update:modelValue', false)
    } catch (e) {
      error.value = errorText(e, 'Could not mark the stock as finished.')
    } finally {
      saving.value = false
    }
  }
</script>
