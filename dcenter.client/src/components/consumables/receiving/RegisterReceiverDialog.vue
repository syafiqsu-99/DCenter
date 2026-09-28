<template>
  <v-dialog :model-value="modelValue" max-width="460" persistent @update:model-value="close">
    <v-card>
      <v-card-item>
        <template #prepend><v-avatar color="primary" variant="tonal" icon="mdi-account-plus-outline" /></template>
        <v-card-title>Register a new name</v-card-title>
        <v-card-subtitle>Adds you to Settings → Welders so you can be picked next time</v-card-subtitle>
      </v-card-item>
      <v-card-text>
        <v-text-field v-model="welderName" label="Name" autofocus v-bind="field" class="mb-3" />
        <v-text-field v-model="welderNo" label="Welder No. / Staff ID" v-bind="field" @keydown.enter.prevent="submit" />
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" :disabled="!canSave" :loading="saving" @click="submit">Register</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { computed, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText } from '@/utils/consumables'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    name: { type: String, default: '' },
  })
  const emit = defineEmits(['update:modelValue', 'registered'])

  const store = useConsumableStore()
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const welderName = ref('')
  const welderNo = ref('')
  const saving = ref(false)
  const error = ref('')

  const canSave = computed(() => !!welderName.value.trim() && !!welderNo.value.trim() && !saving.value)

  watch(() => props.modelValue, (open) => {
    if (!open) return
    welderName.value = props.name.trim()
    welderNo.value = ''
    error.value = ''
  })

  function close(value) {
    if (!value && !saving.value) emit('update:modelValue', false)
  }

  async function submit() {
    if (!canSave.value) return
    saving.value = true
    error.value = ''
    try {
      const welder = await store.registerWelder({
        id: 0, welderName: welderName.value.trim(), welderNo: welderNo.value.trim(), isActive: true, usageScope: 'Report',
      })
      emit('registered', welder)
      emit('update:modelValue', false)
    } catch (e) {
      error.value = errorText(e, 'Could not register this name. Try again.')
    } finally {
      saving.value = false
    }
  }
</script>
