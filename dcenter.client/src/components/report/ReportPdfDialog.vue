<template>
  <v-dialog :model-value="modelValue" fullscreen transition="dialog-bottom-transition"
            @update:model-value="emit('update:modelValue', $event)" @after-leave="release">
    <v-card class="d-flex flex-column">
      <v-toolbar density="comfortable" color="surface">
        <v-toolbar-title class="text-subtitle-1">PDF preview — {{ workOrderNumber }}</v-toolbar-title>
        <v-spacer />
        <v-btn variant="text" prepend-icon="mdi-download" :disabled="!pdfUrl" @click="download">Download</v-btn>
        <v-btn icon="mdi-close" aria-label="Close PDF preview" @click="emit('update:modelValue', false)" />
      </v-toolbar>
      <v-divider />
      <div class="flex-grow-1 d-flex align-center justify-center" style="min-height:0;">
        <iframe v-if="pdfUrl" :src="pdfUrl" title="Report PDF"
                style="width:100%;height:100%;border:0;display:block;" />
        <v-progress-circular v-else-if="loading" indeterminate color="primary" size="48" />
        <v-alert v-else-if="error" type="error" variant="tonal" max-width="480">{{ error }}</v-alert>
      </div>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, watch } from 'vue';
  import api from '@/utils/api';

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    workOrderNumber: { type: String, default: '' },
    partNo: { type: String, default: '' },
    description: { type: String, default: '' },
  });
  const emit = defineEmits(['update:modelValue']);

  const pdfUrl = ref('');
  const loading = ref(false);
  const error = ref('');
  let requestId = 0;

  async function load() {
    const current = ++requestId;
    loading.value = true;
    error.value = '';
    try {
      const res = await api.get(`/reports/${encodeURIComponent(props.workOrderNumber)}/pdf`, { responseType: 'blob' });
      if (current !== requestId || !props.modelValue) return;
      pdfUrl.value = URL.createObjectURL(res.data);
    } catch {
      if (current === requestId) error.value = 'Could not load the PDF.';
    } finally {
      if (current === requestId) loading.value = false;
    }
  }

  function release() {
    requestId++;
    if (pdfUrl.value) URL.revokeObjectURL(pdfUrl.value);
    pdfUrl.value = '';
    error.value = '';
    loading.value = false;
  }

  function shortDesc(desc) {
    return (desc ?? '').trim().split(/\s+/).slice(0, 6).join(' ').slice(0, 40);
  }

  function download() {
    if (!pdfUrl.value) return;
    const name = [props.workOrderNumber, props.partNo, shortDesc(props.description)]
      .map((p) => (p ?? '').toString().trim())
      .filter(Boolean)
      .join(' ')
      .replace(/[\\/:*?"<>|]+/g, '')
      .replace(/\s+/g, ' ')
      .trim() || 'WeldOrderCard';
    const a = document.createElement('a');
    a.href = pdfUrl.value;
    a.download = `${name}.pdf`;
    a.click();
  }

  watch(() => props.modelValue, (open) => {
    if (open && props.workOrderNumber) load();
  }, { immediate: true });
</script>
