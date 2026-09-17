<template>
  <v-app>
    <v-overlay :model-value="booting || bootError" class="align-center justify-center" persistent
               scrim="#ffffff" opacity="1" style="z-index:3000;">
      <div class="text-center" style="max-width:420px;">
        <v-img :src="logonobg" width="160" class="mx-auto mb-6" alt="DCenter" />

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
      <button type="button" class="brand-home d-flex align-center ms-3"
              aria-label="Go to dashboard" @click="goHome">
        <v-img :src="logo" width="40" height="40" class="me-3" alt="" />
        <span class="text-h6">DCenter Operations Hub</span>
      </button>

      <v-spacer />

      <v-btn v-for="item in navItems" :key="item.to"
             :variant="route.path === item.to ? 'tonal' : 'text'"
             :prepend-icon="item.icon"
             class="me-2"
             @click="navigate(item.to)">
        {{ item.label }}
      </v-btn>
    </v-app-bar>

    <v-main>
      <v-container fluid class="pa-4">
        <router-view v-if="!booting && !bootError" />
      </v-container>
    </v-main>

    <v-footer color="primary" app class="text-caption justify-space-between px-4">
      <span>DCenter Operations Hub</span>
      <span>&copy; {{ year }} Emerson — DCenter</span>
    </v-footer>

    <v-dialog v-model="leaveDialog" max-width="480" persistent>
      <v-card>
        <v-card-title>You're editing a report</v-card-title>
        <v-card-text>
          Job <strong>{{ reportStore.report?.jobNumber }}</strong> is still open.
          Save it as a draft, mark it complete, or leave without saving.
          <v-alert v-if="!reportStore.hasDateWelded" type="warning" variant="tonal"
                   density="compact" class="mt-3">
            Date welded is empty, so this report can't be saved or completed yet.
          </v-alert>
        </v-card-text>
        <v-card-actions class="flex-wrap ga-1">
          <v-btn variant="text" @click="leaveDialog = false">Stay</v-btn>
          <v-spacer />
          <v-btn color="error" variant="text" @click="leaveWithoutSaving">Leave without saving</v-btn>
          <v-btn variant="tonal" :disabled="!reportStore.hasDateWelded"
                 :loading="reportStore.saving" @click="leaveAfter('save')">
            Save draft
          </v-btn>
          <v-btn color="success" variant="flat" :disabled="!reportStore.hasDateWelded"
                 @click="leaveAfter('complete')">
            Mark complete
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-app>
</template>

<script setup>
  import { computed, ref } from 'vue';
  import { useRoute, useRouter } from 'vue-router';
  import { useReportStore } from '@/store/reportStore';
  import { useLookupStore } from '@/store/lookupStore';
  import logo from '@/assets/DCenter.png'
  import logonobg from '@/assets/DCenter_No_bg.png';

  const route = useRoute();
  const router = useRouter();
  const navItems = [
    { to: '/', label: 'Report', icon: 'mdi-file-document-edit-outline' },
    { to: '/settings', label: 'Settings', icon: 'mdi-cog-outline' },
  ];

  const year = computed(() => new Date().getFullYear());

  const booting = ref(true);
  const bootError = ref(false);
  const attempt = ref(0);

  const reportStore = useReportStore();
  const lookupStore = useLookupStore();

  const leaveDialog = ref(false);
  const pendingPath = ref('/');

  function navigate(path) {
    pendingPath.value = path;
    if (reportStore.confirmed && route.path === '/') {
      leaveDialog.value = true;
      return;
    }
    if (route.path !== path) router.push(path);
  }

  function goHome() {
    pendingPath.value = '/';
    if (reportStore.confirmed) {
      leaveDialog.value = true;
      return;
    }
    if (route.path !== '/') router.push('/');
  }

  function finishLeaving() {
    leaveDialog.value = false;
    reportStore.backToList();
    if (route.path !== pendingPath.value) router.push(pendingPath.value);
  }

  function leaveWithoutSaving() {
    finishLeaving();
  }

  async function leaveAfter(mode) {
    if (!(await reportStore.save())) return;
    if (mode === 'complete' && !(await reportStore.setComplete(true))) return;
    finishLeaving();
  }

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

<style scoped>
  .brand-home {
    background: none;
    border: 0;
    padding: 0;
    color: inherit;
    cursor: pointer;
  }

    .brand-home:focus-visible {
      outline: 2px solid currentColor;
      outline-offset: 4px;
      border-radius: 4px;
    }
</style>
