<template>
  <v-card flat border class="mb-8">
    <v-card-actions>
      <v-btn color="secondary" :loading="saving" prepend-icon="mdi-content-save" @click="saveOnly">
        Save draft
      </v-btn>
      <v-btn color="success" variant="tonal" class="ml-2" prepend-icon="mdi-check-circle"
             @click="markComplete">
        Mark complete
      </v-btn>
      <v-spacer />
      <v-btn color="primary" variant="tonal" prepend-icon="mdi-file-pdf-box" @click="viewPdf">
        View PDF
      </v-btn>
      <v-btn color="primary" prepend-icon="mdi-microsoft-excel" @click="downloadExcel">
        Download Excel
      </v-btn>
    </v-card-actions>
  </v-card>

  <v-snackbar v-model="snackbar" color="success" timeout="2500">Draft saved.</v-snackbar>
</template>

<script setup>
  import { ref } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useReportStore } from '@/store/reportStore';
  import api from '@/utils/api';

  const store = useReportStore();
  const { report, saving } = storeToRefs(store);
  const snackbar = ref(false);
  const pdfUrl = ref('');

  async function saveOnly() {
        if (await store.save()) snackbar.value = true;
  }

  async function markComplete() {
        if (!(await store.save())) return;
        await store.setComplete(true);
        snackbar.value = true;
  }

  async function viewPdf() {
        if (!(await store.save())) return;
        const res = await api.get(`/reports/${encodeURIComponent(report.value.jobNumber)}/pdf`, {
          responseType: 'blob',
        });
        pdfUrl.value = URL.createObjectURL(res.data);
        window.open(pdfUrl.value, '_blank');
  }

  async function downloadExcel() {
        if (!(await store.save())) return;
        const res = await api.get(`/reports/${encodeURIComponent(report.value.jobNumber)}/excel`, {
          responseType: 'blob',
        });
        const url = URL.createObjectURL(res.data);
        const a = document.createElement('a');
        a.href = url;
        a.download = `WeldOrderCard_${report.value.jobNumber}.xlsx`;
        a.click();
        URL.revokeObjectURL(url);
  }
</script>
