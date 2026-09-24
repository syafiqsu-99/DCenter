<template>
  <v-card flat class="settings-panel-card">
    <v-card-title>Supervisor password</v-card-title>
    <v-card-subtitle class="text-wrap">
      One shared password unlocks supervisor features (Settings and consumable management). Everyone who knows it will need the new one.
    </v-card-subtitle>
    <v-card-text style="max-width:520px;">
      <v-alert v-if="status" :type="status.source === 'Database' ? 'info' : 'warning'" variant="tonal" density="compact" class="mb-4">
        <template v-if="status.source === 'Database'">
          Last changed {{ fmtDateTime(status.updatedAt) }} by {{ status.updatedBy }}.
        </template>
        <template v-else>
          Still using the initial password set by IT. Change it here so it is stored securely in the database.
        </template>
      </v-alert>

      <v-form ref="form" @submit.prevent="submit">
        <v-text-field v-model="current" :type="show ? 'text' : 'password'" label="Current password" v-bind="field"
                      autocomplete="current-password" class="mb-3" />
        <v-text-field v-model="next" :type="show ? 'text' : 'password'" label="New password" v-bind="field"
                      autocomplete="new-password" :rules="[rules.length, rules.edges, rules.different]" class="mb-3"
                      :hint="`${MIN_LENGTH} or more characters`" persistent-hint />
        <v-text-field v-model="confirm" :type="show ? 'text' : 'password'" label="Confirm new password" v-bind="field"
                      autocomplete="new-password" :rules="[rules.match]" class="mb-2" />
        <v-checkbox v-model="show" label="Show passwords" density="compact" hide-details />
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
        <div class="d-flex mt-4">
          <v-spacer />
          <v-btn type="submit" color="primary" variant="flat" prepend-icon="mdi-key-change" :loading="saving" :disabled="!canSave">
            Change password
          </v-btn>
        </div>
      </v-form>
    </v-card-text>
  </v-card>
  <v-snackbar v-model="snackbar" color="success" timeout="4000">Supervisor password changed.</v-snackbar>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, fmtDateTime } from '@/utils/consumables'

  const MIN_LENGTH = 8
  const MAX_LENGTH = 128

  const store = useConsumableStore()
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const form = ref(null)
  const status = ref(null)
  const current = ref('')
  const next = ref('')
  const confirm = ref('')
  const show = ref(false)
  const saving = ref(false)
  const error = ref('')
  const snackbar = ref(false)

  const rules = {
    length: (v) => (v.length >= MIN_LENGTH && v.length <= MAX_LENGTH) || `Use ${MIN_LENGTH} to ${MAX_LENGTH} characters.`,
    edges: (v) => v.trim().length === v.length || 'Cannot start or end with a space.',
    different: (v) => !v || v !== current.value || 'Must differ from the current password.',
    match: (v) => v === next.value || 'Passwords do not match.',
  }

  const canSave = computed(() =>
    !!current.value && [rules.length, rules.edges, rules.different].every((r) => r(next.value) === true) && confirm.value === next.value)

  async function loadStatus() {
    try {
      status.value = await store.loadPasswordStatus()
    } catch (e) {
      error.value = errorText(e, 'Could not load the password status.')
    }
  }

  async function submit() {
    if (!canSave.value || saving.value) return
    saving.value = true
    error.value = ''
    try {
      status.value = await store.changePassword(current.value, next.value)
      current.value = ''
      next.value = ''
      confirm.value = ''
      form.value?.resetValidation()
      snackbar.value = true
    } catch (e) {
      error.value = errorText(e, 'Could not change the password.')
    } finally {
      saving.value = false
    }
  }

  onMounted(loadStatus)
</script>
