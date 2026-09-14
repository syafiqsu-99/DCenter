<template>
  <v-app>
    <v-overlay :model-value="booting || bootError" class="align-center justify-center" persistent
               scrim="#ffffff" opacity="1" style="z-index:3000;">
      <div class="text-center" style="max-width:420px;">
        <v-img :src="logo" width="160" class="mx-auto mb-6" />

        <template v-if="!bootError">
          <v-progress-circular color="primary" indeterminate size="42" width="4" />
          <div class="mt-4 text-medium-emphasis">
            {{ attempt <= 1 ? 'Loading…' : `Connecting to server… (attempt ${attempt})` }}
          </div>
        </template>

        <template v-else>
          <v-icon color="error" size="42">mdi-lan-disconnect</v-icon>
          <div class="mt-4 mb-1 font-weight-medium">Can't reach the server</div>
          <div class="text-medium-emphasis text-body-2 mb-4">
            The API isn't responding. Make sure the backend (DCenter.Server) is running,
            then retry.
          </div>
          <v-btn color="primary" prepend-icon="mdi-refresh" @click="boot">Retry</v-btn>
        </template>
      </div>
    </v-overlay>

    <v-app-bar color="primary" flat>
      <v-img :src="logo" max-width="34" class="ms-3 me-2" />
      <v-app-bar-title>Weld Report System</v-app-bar-title>
      <v-tabs v-model="tab" align-tabs="end" slider-color="white">
        <v-tab value="report" to="/" prepend-icon="mdi-file-document-edit-outline">Report</v-tab>
        <v-tab value="settings" to="/settings" prepend-icon="mdi-cog-outline">Settings</v-tab>
      </v-tabs>
    </v-app-bar>

    <v-main>
      <v-container fluid>
        <router-view />
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup>
    import { ref } from 'vue';
    import { useReportStore } from '@/store/reportStore';
    import { useLookupStore } from '@/store/lookupStore';
    import logo from '@/assets/DCenter.png';

    const tab = ref('report');
    const booting = ref(true);
    const bootError = ref(false);
    const attempt = ref(0);

    const reportStore = useReportStore();
    const lookupStore = useLookupStore();

    const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

    async function boot() {
      booting.value = true;
      bootError.value = false;
      const maxAttempts = 8;
      const delays = [500, 1000, 2000, 3000, 4000, 5000, 5000];

      for (let i = 0; i < maxAttempts; i++) {
        attempt.value = i + 1;
        try {
          await Promise.all([
            reportStore.loadRows(),
            reportStore.loadSavedReports(),
            lookupStore.load(true),
          ]);
          booting.value = false;
          return;
        } catch {
          if (i < maxAttempts - 1) {
            await sleep(delays[i] ?? 5000);
          }
        }
      }
      booting.value = false;
      bootError.value = true;
    }

    boot();
</script>
