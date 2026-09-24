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
              aria-label="Go to dashboard" @click="onGoHome">
        <v-img :src="logo" width="40" height="40" class="me-3" alt="" />
        <span class="text-h6">DCenter Operations Hub</span>
      </button>

      <v-spacer />

      <v-btn v-for="item in navItems" :key="item.to"
             :variant="route.path === item.to || (item.to !== '/' && route.path.startsWith(item.to)) ? 'tonal' : 'text'"
             :prepend-icon="item.icon"
             class="me-2"
             @click="onNav(item.to)">
        {{ item.label }}
      </v-btn>

      <SupervisorNavButton v-if="!booting && !bootError" />
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

    <v-dialog v-model="leaveDialog" max-width="500" persistent>
      <v-card title="Unsaved changes" prepend-icon="mdi-content-save-alert">
        <v-divider />
        <v-card-text>
          Work order <strong>{{ reportStore.report?.workOrderNumber || '(new report)' }}</strong>
          has changes that aren't saved yet.
          <v-alert v-if="!reportStore.report?.workOrderNumber" type="warning" variant="tonal"
                   density="compact" class="mt-3">
            Enter a work order number before saving — or leave without saving.
          </v-alert>
          <v-alert v-else-if="!reportStore.hasDateWelded" type="warning" variant="tonal"
                   density="compact" class="mt-3">
            Date welded is empty, so this report can't be saved or completed yet — you can
            still leave without saving.
          </v-alert>
        </v-card-text>
        <v-divider />
        <v-card-actions class="px-4 py-3 flex-wrap ga-1">
          <v-btn variant="text" @click="stay">Stay</v-btn>
          <v-spacer />
          <v-btn color="error" variant="text" @click="discardAndProceed">Leave without saving</v-btn>
          <template v-if="reportStore.report?.workOrderNumber && reportStore.hasDateWelded">
            <v-btn variant="tonal" :loading="reportStore.saving" @click="saveAndProceed(false)">
              Save draft
            </v-btn>
            <v-btn color="success" variant="flat" :loading="reportStore.saving"
                   @click="saveAndProceed(true)">
              Mark complete
            </v-btn>
          </template>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-app>
</template>

<script setup>
  import { onMounted, onBeforeUnmount, computed, ref } from 'vue';
  import { useRoute, useRouter } from 'vue-router';
  import { useReportStore } from '@/store/reportStore';
  import { useLookupStore } from '@/store/lookupStore';
  import { useConsumableStore } from '@/store/consumableStore';
  import logo from '@/assets/DCenter.png'
  import logonobg from '@/assets/DCenter_No_bg.png';
  import { useLeaveGuard } from '@/composables/useLeaveGuard';
  import SupervisorNavButton from '@/components/common/SupervisorNavButton.vue';

  const route = useRoute();
  const router = useRouter();
  const consumableStore = useConsumableStore();
  const allNavItems = [
    { to: '/', label: 'Report', icon: 'mdi-file-document-edit-outline' },
    { to: '/consumables', label: 'Consumables', icon: 'mdi-package-variant-closed' },
    { to: '/settings', label: 'Settings', icon: 'mdi-cog-outline', supervisor: true },
  ];
  const navItems = computed(() => allNavItems.filter((item) => !item.supervisor || consumableStore.isSupervisor));

  const year = computed(() => new Date().getFullYear());

  const booting = ref(true);
  const bootError = ref(false);
  const attempt = ref(0);

  const reportStore = useReportStore();
  const lookupStore = useLookupStore();
  const { leaveDialog, hold, guardLeave, stay, discardAndProceed, saveAndProceed } = useLeaveGuard();

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

  function onGoHome() {
    onNav('/')
  }
  function onNav(path) {
    if (path === route.path) {
      if (path === '/' && reportStore.confirmed) guardLeave(() => reportStore.backToList())
      return
    }
    router.push(path)
  }

  let bypass = false
  const removeGuard = router.beforeEach((to, from) => {
    if (bypass) { bypass = false; return true }
    if (from.path === '/' && to.path !== '/' && reportStore.confirmed) {
      if (reportStore.needsLeavePrompt) {
        hold(() => { reportStore.backToList(); bypass = true; router.push(to.fullPath) })
        return false
      }
      reportStore.backToList()
    }
    return true
  })

  function onBeforeUnload(e) {
    if (reportStore.needsLeavePrompt) {
      e.preventDefault()
      e.returnValue = ''
    }
  }

  consumableStore.init();

  onMounted(() => window.addEventListener('beforeunload', onBeforeUnload))
  onBeforeUnmount(() => {
    window.removeEventListener('beforeunload', onBeforeUnload)
    removeGuard()
  });

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
