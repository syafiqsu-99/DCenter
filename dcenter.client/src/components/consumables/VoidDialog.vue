<template>
  <v-dialog :model-value="modelValue" max-width="520" persistent @update:model-value="close">
    <v-card v-if="transaction" prepend-icon="mdi-cancel" :title="`Void ${transaction.txnNo}?`">
      <v-card-text>
        <p class="text-body-2 mb-3">
          This cancels every line of <strong>{{ transaction.txnNo }}</strong>
          ({{ TXN_LABELS[transaction.txnType] ?? transaction.txnType }} · {{ transaction.diaSpec }}).
          Both entries stay in History.
        </p>
        <v-textarea v-model="remarks" label="Reason for voiding" rows="2" auto-grow variant="outlined"
                    density="comfortable" hide-details="auto" autofocus />
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close(false)">Keep</v-btn>
        <v-btn color="error" variant="flat" :loading="saving" :disabled="!remarks.trim() || !store.hasEnteredBy" @click="submit">
          Void entry
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { TXN_LABELS, errorText } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    transaction: { type: Object, default: null },
  })
  const emit = defineEmits(['update:modelValue', 'voided'])

  const store = useConsumableStore()
  const remarks = ref('')
  const saving = ref(false)
  const error = ref('')

  watch(() => props.modelValue, (open) => {
    if (!open) return
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
      const result = await store.voidTransaction(props.transaction.txnNo, remarks.value.trim())
      emit('voided', result)
      close(false)
    } catch (e) {
      error.value = errorText(e, 'Could not void this entry.')
    } finally {
      saving.value = false
    }
  }
</script>
