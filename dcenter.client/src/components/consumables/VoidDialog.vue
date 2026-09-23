<template>
  <v-dialog :model-value="modelValue" max-width="480" persistent
            @update:model-value="emit('update:modelValue', $event)">
    <v-card title="Void this entry?" prepend-icon="mdi-cancel">
      <v-divider />
      <v-card-text v-if="transaction">
        <div class="mb-3">
          <TxnTypeChip :type="transaction.txnType" class="me-2" />
          <strong>{{ kg(Math.abs(transaction.quantityKg)) }} kg</strong>
          · {{ transaction.diaSpec }} · Lot {{ transaction.lotNumber }} · {{ transaction.location }}
          <div class="text-caption text-medium-emphasis">
            {{ fmtDate(transaction.txnDate) }} · {{ transaction.requestor || '—' }}
          </div>
        </div>
        <v-textarea v-model="remarks" label="Reason (required)" rows="2" auto-grow variant="outlined"
                    density="comfortable" hide-details="auto" autofocus />
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-4 py-3">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close">Cancel</v-btn>
        <v-btn color="error" variant="flat" :loading="saving" :disabled="!remarks.trim()" @click="confirm">Void</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, fmtDate, kg } from '@/utils/consumables'
  import TxnTypeChip from '@/components/consumables/TxnTypeChip.vue'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    transaction: { type: Object, default: null },
  })
  const emit = defineEmits(['update:modelValue', 'voided'])

  const store = useConsumableStore()
  const remarks = ref('')
  const error = ref('')
  const saving = ref(false)

  watch(() => props.modelValue, (open) => {
    if (open) {
      remarks.value = ''
      error.value = ''
    }
  })

  function close() {
    emit('update:modelValue', false)
  }

  async function confirm() {
    saving.value = true
    error.value = ''
    try {
      const result = await store.voidTransaction(props.transaction.id, remarks.value.trim())
      emit('voided', result)
      close()
    } catch (e) {
      error.value = errorText(e, 'Could not void this entry.')
    } finally {
      saving.value = false
    }
  }
</script>
