<template>
  <v-dialog :model-value="modelValue" max-width="420" persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card prepend-icon="mdi-shield-lock-outline" title="Supervisor login" subtitle="Unlocks Settings and consumable management">
      <v-divider />
      <v-card-text>
        <v-form @submit.prevent="submit">
          <v-combobox v-model="name" :items="people" item-title="welderName" item-value="welderName" :return-object="false"
                      :loading="loadingPeople" label="Your name" prepend-inner-icon="mdi-account" variant="outlined"
                      density="comfortable" hint="Choose your name, or type it if you are not in the list" persistent-hint
                      class="mb-3" autocomplete="off" @update:search="onSearch">
            <template #item="{ props: p, item }">
              <v-list-item v-bind="p" :subtitle="item.raw.welderNo" />
            </template>
          </v-combobox>
          <v-text-field ref="passwordField" v-model="password" :type="show ? 'text' : 'password'" label="Supervisor password"
                        prepend-inner-icon="mdi-key" :append-inner-icon="show ? 'mdi-eye-off' : 'mdi-eye'" variant="outlined"
                        density="comfortable" hide-details="auto" autocomplete="current-password"
                        @click:append-inner="show = !show" />
          <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
          <button type="submit" hidden />
        </v-form>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="busy" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" :loading="busy" :disabled="!(name ?? '').toString().trim() || !password" @click="submit">
          Login
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { nextTick, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { debounce, errorText } from '@/utils/consumables'

  const NAME_KEY = 'dcenter.consumables.supervisorName'

  const props = defineProps({ modelValue: { type: Boolean, default: false } })
  const emit = defineEmits(['update:modelValue', 'unlocked'])

  const store = useConsumableStore()
  const name = ref('')
  const password = ref('')
  const show = ref(false)
  const busy = ref(false)
  const error = ref('')
  const passwordField = ref(null)
  const people = ref([])
  const loadingPeople = ref(false)
  let searchToken = 0

  const searchPeople = debounce(async (q) => {
    const token = ++searchToken
    loadingPeople.value = true
    try {
      const result = await store.searchWelders(q ?? '', false)
      if (token === searchToken) people.value = result
    } catch {
      if (token === searchToken) people.value = []
    } finally {
      if (token === searchToken) loadingPeople.value = false
    }
  }, 250)

  function onSearch(q) {
    if (q && q === name.value) return
    searchPeople(q)
  }

  watch(() => props.modelValue, async (open) => {
    if (!open) return
    name.value = localStorage.getItem(NAME_KEY) ?? ''
    searchPeople('')
    password.value = ''
    error.value = ''
    show.value = false
    await nextTick()
    if (name.value) passwordField.value?.focus()
  })

  async function submit() {
    name.value = (name.value ?? '').toString()
    if (!name.value.trim() || !password.value || busy.value) return
    busy.value = true
    error.value = ''
    try {
      await store.unlockSupervisor(name.value.trim(), password.value)
      localStorage.setItem(NAME_KEY, name.value.trim())
      emit('update:modelValue', false)
      emit('unlocked')
    } catch (e) {
      error.value = errorText(e, 'Could not log in.')
      password.value = ''
    } finally {
      busy.value = false
    }
  }
</script>
