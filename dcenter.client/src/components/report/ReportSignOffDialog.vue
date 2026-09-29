<template>
  <v-dialog :model-value="modelValue" max-width="640" scrollable persistent
            @update:model-value="emit('update:modelValue', $event)">
    <v-card prepend-icon="mdi-draw-pen" title="Sign-off" :subtitle="`Printed on every joint of ${workOrderNumber}`">
      <v-divider />
      <v-card-text>
        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />
        <v-alert v-else-if="error" type="error" variant="tonal" density="compact" class="mb-4">{{ error }}</v-alert>
        <template v-if="form">
          <div class="text-subtitle-2 mb-2">Engineer / Supervisor</div>
          <v-row dense>
            <v-col cols="12" sm="8">
              <v-text-field v-model="form.engineerName" label="Name" v-bind="field" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="form.engineerDate" label="Date" type="date" v-bind="field" />
            </v-col>
          </v-row>

          <div class="text-subtitle-2 mt-4 mb-2">QA Inspector</div>
          <v-row dense>
            <v-col cols="12" sm="8">
              <v-text-field v-model="form.qaName" label="Name" v-bind="field" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="form.qaDate" label="Date" type="date" v-bind="field" />
            </v-col>
          </v-row>

          <div class="text-subtitle-2 mt-4 mb-2">Welder</div>
          <div v-if="!form.welders.length" class="text-medium-emphasis text-body-2">No welder is set on any joint.</div>
          <v-row v-for="w in form.welders" :key="`${w.welderName}|${w.welderNo}`" dense>
            <v-col cols="12" sm="8">
              <v-text-field v-model="w.displayName" label="Name" v-bind="field" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="w.date" label="Date" type="date" v-bind="field" />
            </v-col>
          </v-row>
        </template>
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-4 py-3">
        <v-spacer />
        <v-btn variant="text" :disabled="busy" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" :prepend-icon="confirmIcon" :loading="busy" :disabled="!form" @click="confirm">
          {{ confirmLabel }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, watch } from 'vue'
  import { useReportStore } from '@/store/reportStore'
  import { todayIso } from '@/utils/date'

  const REMEMBER_KEY = 'dcenter.report.signoff'
  const MAX_NAME = 100

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    workOrderNumber: { type: String, default: '' },
    confirmLabel: { type: String, default: 'Download' },
    confirmIcon: { type: String, default: 'mdi-download' },
    busy: { type: Boolean, default: false },
  })
  const emit = defineEmits(['update:modelValue', 'confirm'])

  const store = useReportStore()
  const form = ref(null)
  const loading = ref(false)
  const error = ref('')
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: true, maxlength: MAX_NAME }
  let requestId = 0

  function remembered() {
    try {
      return JSON.parse(localStorage.getItem(REMEMBER_KEY) ?? '{}') ?? {}
    } catch {
      return {}
    }
  }

  function remember(engineerName, qaName) {
    try {
      localStorage.setItem(REMEMBER_KEY, JSON.stringify({ engineerName, qaName }))
    } catch {
      // private window or storage disabled: nothing to remember
    }
  }

  async function load() {
    const current = ++requestId
    form.value = null
    loading.value = true
    error.value = ''
    try {
      const d = await store.fetchSignOff(props.workOrderNumber)
      if (current !== requestId) return
      const saved = remembered()
      form.value = {
        engineerName: saved.engineerName || d.engineerName || '',
        engineerDate: d.engineerDate || todayIso(),
        qaName: saved.qaName || d.qaName || '',
        qaDate: d.qaDate || todayIso(),
        welders: (d.welders ?? []).map((w) => ({ ...w, displayName: w.displayName ?? '', date: w.date ?? '' })),
      }
    } catch {
      if (current === requestId) error.value = 'Could not load the sign-off details. Close this dialog and try again.'
    } finally {
      if (current === requestId) loading.value = false
    }
  }

  function confirm() {
    const f = form.value
    if (!f) return
    remember(f.engineerName.trim(), f.qaName.trim())
    emit('confirm', {
      engineerName: f.engineerName,
      engineerDate: f.engineerDate || null,
      qaName: f.qaName,
      qaDate: f.qaDate || null,
      welders: f.welders.map((w) => ({
        welderName: w.welderName, welderNo: w.welderNo, displayName: w.displayName, date: w.date || null,
      })),
    })
  }

  watch(() => props.modelValue, (open) => {
    if (open && props.workOrderNumber) load()
  }, { immediate: true })
</script>
